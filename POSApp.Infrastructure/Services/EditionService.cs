using System.Net.NetworkInformation;
using System.Text;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using Windows.Services.Store;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// Resolves the running edition and, in the Store build, checks/sells the "Pro"
    /// subscription through <see cref="StoreContext"/>.
    ///
    /// <list type="bullet">
    /// <item>Not packaged (Inno Setup / dotnet run) → <see cref="AppEdition.Direct"/>: every feature, yearly licence gate.</item>
    /// <item>Packaged (MSIX) → <see cref="AppEdition.StoreLite"/>, or <see cref="AppEdition.StorePro"/> while a Pro subscription is active.</item>
    /// </list>
    ///
    /// Any add-on whose Product ID starts with <see cref="ProProductIdPrefix"/> grants Pro, so
    /// several plans can be sold side by side (e.g. <c>pro_monthly</c> and <c>pro_yearly</c>).
    /// When a subscription lapses the Store marks its licence inactive and the app falls back to
    /// Lite — data is never touched, Pro screens just ask to renew.
    ///
    /// Debug builds honour <c>POSAPP_EDITION=direct|lite|pro</c> so the Store behaviour can be
    /// tried without packaging. Release builds ignore it — otherwise it would be a way around
    /// the direct edition's licence.
    /// </summary>
    public sealed class EditionService : IEditionService
    {
        /// <summary>
        /// Product IDs (InAppOfferToken) of Pro subscription add-ons in Partner Center must start
        /// with this prefix, e.g. <c>pro_monthly</c>, <c>pro_yearly</c>.
        /// </summary>
        public const string ProProductIdPrefix = "pro_";

        /// <summary>
        /// Partner Center product IDs this app asks the Store for. Any other Durable add-on whose
        /// token starts with <see cref="ProProductIdPrefix"/> and has a subscription SKU is accepted too.
        /// </summary>
        public static readonly string[] RequestedProProductIds = { "pro_monthly", "pro_yearly" };

        /// <summary>Subscription add-ons are returned under the Store's Durable product kind.</summary>
        public static readonly string[] RequestedProductKinds = { "Durable" };

        public static bool IsProProductId(string? productId) =>
            !string.IsNullOrEmpty(productId) &&
            productId.StartsWith(ProProductIdPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>A plan the Subscribe button may buy: a Pro product id with a paid subscription SKU.</summary>
        public static bool IsPurchasableProPlan(string? productId, bool hasNonTrialSubscriptionSku) =>
            IsProProductId(productId) && hasNonTrialSubscriptionSku;

        private readonly bool _isStoreBuild;
        private StoreContext? _context;
        private AppEdition _edition;
        private readonly Dictionary<string, StoreProduct> _products = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler? EditionChanged;

        public DateTimeOffset? ProExpiresOn { get; private set; }

        public EditionService()
        {
            _isStoreBuild = AppEnvironment.IsPackaged;
            _edition = _isStoreBuild ? AppEdition.StoreLite : AppEdition.Direct;

#if DEBUG
            switch (Environment.GetEnvironmentVariable("POSAPP_EDITION")?.Trim().ToLowerInvariant())
            {
                case "direct": _isStoreBuild = false; _edition = AppEdition.Direct; _debugForced = true; break;
                case "lite":   _isStoreBuild = true;  _edition = AppEdition.StoreLite; _debugForced = true; break;
                case "pro":    _isStoreBuild = true;  _edition = AppEdition.StorePro; _debugForced = true; break;
            }
#endif
        }

#if DEBUG
        private readonly bool _debugForced;
#else
        private const bool _debugForced = false;
#endif

        public AppEdition Edition => _edition;

        public bool IsEnabled(AppFeature feature) => EditionPolicy.IsEnabled(_edition, feature);

        public bool IsInBuild(AppFeature feature) => EditionPolicy.IsInBuild(_edition, feature);

        public async Task RefreshAsync()
        {
            if (!_isStoreBuild || _debugForced || !AppEnvironment.IsPackaged) return;

            try
            {
                var license = await GetContext().GetAppLicenseAsync();
                var active = license.AddOnLicenses.Values
                    .Where(a => a.IsActive && IsProProductId(a.InAppOfferToken))
                    .ToList();

                ProExpiresOn = active.Count > 0 ? active.Max(a => a.ExpirationDate) : null;
                SetEdition(active.Count > 0 ? AppEdition.StorePro : AppEdition.StoreLite);
            }
            catch
            {
                // Store unreachable and no cached licence: stay on the current tier.
                // Windows caches Store licences, so a subscriber who is offline keeps Pro.
            }
        }

        public async Task<ProOfferQuery> GetProOffersAsync(IntPtr ownerWindowHandle)
        {
            if (!_isStoreBuild) return Empty(StorePlanProblem.CatalogEmpty);

#if DEBUG
            if (_debugForced)
            {
                LogStore("debug forced edition; built-in plans returned; Store was not called");
                return new ProOfferQuery(DebugOffers(), StorePlanProblem.None, null);
            }
#endif
            if (!AppEnvironment.IsPackaged) return Empty(StorePlanProblem.CatalogEmpty);

            var log = new StringBuilder();
            var networkAvailable = TryNetworkAvailable();
            log.AppendLine("store catalog");
            log.AppendLine($"networkAvailable={(networkAvailable?.ToString() ?? "unknown")}");
            log.AppendLine($"packaged={AppEnvironment.IsPackaged}");
            log.AppendLine($"hwndSet={ownerWindowHandle != IntPtr.Zero}");
            log.AppendLine($"hwnd=0x{ownerWindowHandle.ToInt64():X}");
            log.AppendLine("productKinds=" + string.Join(",", RequestedProductKinds));
            log.AppendLine($"prefix={ProProductIdPrefix}");
            log.AppendLine("requestedProductIds=" + string.Join(",", RequestedProProductIds));

            ProOfferQuery result;
            try
            {
                var context = GetContext();
                log.AppendLine("storeContext=created");
                if (ownerWindowHandle != IntPtr.Zero)
                    WinRT.Interop.InitializeWithWindow.Initialize(context, ownerWindowHandle);
                else
                    log.AppendLine("warning=no window handle; the Store cannot prompt for sign-in");

                var query = await context.GetAssociatedStoreProductsAsync(RequestedProductKinds);
                result = ReadQuery(query, networkAvailable, log);
            }
            catch (Exception ex)
            {
                var problem = StorePlanClassifier.FromException(ex);
                if (problem == StorePlanProblem.Other && ex.HResult == StorePlanClassifier.StoreUnexpected && networkAvailable == false)
                    problem = StorePlanProblem.Network;
                log.AppendLine("exception=" + ex);
                log.AppendLine($"problem={problem}");
                log.AppendLine("error=" + StorePlanClassifier.FormatCode(ex.HResult));
                _products.Clear();
                result = new ProOfferQuery(Array.Empty<ProOffer>(), problem, StorePlanClassifier.FormatCode(ex.HResult));
            }

            LogStore(log.ToString());
            return result;
        }

        public async Task<UpgradeResult> RequestUpgradeAsync(IntPtr ownerWindowHandle, string offerId)
        {
            if (!_isStoreBuild) return UpgradeResult.NotAvailable;
            if (_edition == AppEdition.StorePro) return UpgradeResult.AlreadyOwned;

#if DEBUG
            if (_debugForced)
            {
                ProExpiresOn = DateTimeOffset.Now.AddMonths(1);
                SetEdition(AppEdition.StorePro);
                return UpgradeResult.Purchased;
            }
#endif
            if (!AppEnvironment.IsPackaged) return UpgradeResult.NotAvailable;

            try
            {
                var context = GetContext();
                // A desktop (non-UWP) app must tell the Store which window owns its dialog.
                WinRT.Interop.InitializeWithWindow.Initialize(context, ownerWindowHandle);

                if (_products.Count == 0) await GetProOffersAsync(ownerWindowHandle);
                if (!_products.TryGetValue(offerId, out var product)) return UpgradeResult.NotAvailable;

                // Offer the free-trial SKU to people who haven't used it yet; the Store handles
                // eligibility and falls back to the paid SKU in its own dialog.
                var trialSku = product.Skus.FirstOrDefault(s => s.IsTrial && !s.IsInUserCollection);
                var result = trialSku != null
                    ? await trialSku.RequestPurchaseAsync()
                    : await product.RequestPurchaseAsync();

                LogStore($"purchase offerId={offerId} status={result.Status} extended={Describe(result.ExtendedError)}");
                switch (result.Status)
                {
                    case StorePurchaseStatus.Succeeded:
                    case StorePurchaseStatus.AlreadyPurchased:
                        await RefreshAsync();
                        if (_edition != AppEdition.StorePro) SetEdition(AppEdition.StorePro);
                        return result.Status == StorePurchaseStatus.Succeeded ? UpgradeResult.Purchased : UpgradeResult.AlreadyOwned;
                    case StorePurchaseStatus.NotPurchased:
                        return UpgradeResult.Cancelled;
                    default:
                        return UpgradeResult.Failed;
                }
            }
            catch (Exception ex)
            {
                LogStore("purchase threw" + Environment.NewLine + ex);
                return UpgradeResult.Failed;
            }
        }

        private ProOfferQuery ReadQuery(StoreProductQueryResult query, bool? networkAvailable, StringBuilder log)
        {
            Exception? extended = null;
            try
            {
                extended = query.ExtendedError;
            }
            catch (Exception ex)
            {
                log.AppendLine("extendedError read failed");
                log.AppendLine(ex.ToString());
            }

            List<StoreProduct> returned;
            try
            {
                returned = query.Products?.Values.ToList() ?? new List<StoreProduct>();
            }
            catch (Exception ex)
            {
                log.AppendLine("reading products threw");
                log.AppendLine(ex.ToString());
                _products.Clear();
                var failureCode = extended?.HResult ?? ex.HResult;
                var problem = StorePlanClassifier.Classify(failureCode, 0, networkAvailable);
                log.AppendLine($"problem={problem}");
                log.AppendLine("error=" + StorePlanClassifier.FormatCode(failureCode));
                return new ProOfferQuery(Array.Empty<ProOffer>(), problem, StorePlanClassifier.FormatCode(failureCode));
            }

            log.AppendLine("extendedError=" + Describe(extended));
            log.AppendLine($"returnedCount={returned.Count}");
            _products.Clear();

            var offers = new List<ProOffer>();
            foreach (var product in returned)
            {
                try
                {
                    var paid = product.Skus.FirstOrDefault(s => s.IsSubscription && !s.IsTrial);
                    var accepted = IsPurchasableProPlan(product.InAppOfferToken, paid != null);
                    log.AppendLine(
                        $"product storeId={product.StoreId} token={product.InAppOfferToken} title={product.Title} " +
                        $"subscriptionSku={(paid != null)} accepted={accepted} price={product.Price.FormattedPrice}");

                    if (!accepted || paid == null) continue;

                    _products[product.StoreId] = product;
                    var trial = product.Skus.FirstOrDefault(s => s.IsTrial);
                    var trialText = trial?.SubscriptionInfo is { } t && t.HasTrialPeriod
                        ? $"{t.TrialPeriod} {Unit(t.TrialPeriodUnit, t.TrialPeriod)} free trial"
                        : null;
                    offers.Add(new ProOffer(product.StoreId, product.Title, FormatPrice(paid), trialText));
                }
                catch (Exception ex)
                {
                    log.AppendLine($"product read failed storeId={product.StoreId}");
                    log.AppendLine(ex.ToString());
                }
            }

            // Cheapest-per-period plans first is not knowable without FX maths; keep monthly before yearly.
            offers = offers
                .OrderBy(o => o.Title.Contains("year", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToList();

            int? hresult = extended?.HResult;
            var classified = StorePlanClassifier.Classify(hresult, offers.Count, networkAvailable);
            var code = extended == null ? null : StorePlanClassifier.FormatCode(extended.HResult);
            log.AppendLine($"purchasable={offers.Count}");
            log.AppendLine($"problem={classified}");
            log.AppendLine("error=" + (code ?? "(none)"));
            return new ProOfferQuery(offers, classified, code);
        }

        private static string FormatPrice(StoreSku sku)
        {
            var info = sku.SubscriptionInfo;
            string? period = null;
            if (info != null && info.BillingPeriod > 0)
            {
                period = info.BillingPeriod == 1
                    ? Unit(info.BillingPeriodUnit, 1)
                    : $"{info.BillingPeriod} {Unit(info.BillingPeriodUnit, info.BillingPeriod)}";
            }

            return StorePriceText.Format(sku.Price.FormattedRecurrencePrice, sku.Price.FormattedPrice, period);
        }

        private static ProOfferQuery Empty(StorePlanProblem problem) =>
            new(Array.Empty<ProOffer>(), problem, null);

#if DEBUG
        private static ProOffer[] DebugOffers() => new[]
        {
            new ProOffer("debug-monthly", "Swifttill Pro — Monthly", "$1.99/month", "1 month free trial"),
            new ProOffer("debug-yearly", "Swifttill Pro — Yearly", "$19.99/year", null)
        };
#endif

        private static bool? TryNetworkAvailable()
        {
            try { return NetworkInterface.GetIsNetworkAvailable(); }
            catch { return null; }
        }

        private static string Describe(Exception? error)
        {
            if (error == null) return "(none)";
            return StorePlanClassifier.FormatCode(error.HResult) + " " + error;
        }

        private static void LogStore(string text)
        {
            try
            {
                var path = Path.Combine(AppPaths.LogsDirectory, "store-catalog.log");
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 1024 * 1024)
                    File.Move(path, path + ".old", overwrite: true);
                File.AppendAllText(path,
                    $"==== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}{text}{Environment.NewLine}");
            }
            catch
            {
                // A log failure must not replace the Store result the dialog is about to show.
            }
        }

        private static string Unit(StoreDurationUnit unit, uint count)
        {
            var s = unit switch
            {
                StoreDurationUnit.Minute => "minute",
                StoreDurationUnit.Hour => "hour",
                StoreDurationUnit.Day => "day",
                StoreDurationUnit.Week => "week",
                StoreDurationUnit.Month => "month",
                StoreDurationUnit.Year => "year",
                _ => "period"
            };
            return count == 1 ? s : s + "s";
        }

        private StoreContext GetContext()
        {
            if (_context != null) return _context;
            _context = StoreContext.GetDefault();
            // Renewals, lapses, refunds and purchases on another device arrive here.
            _context.OfflineLicensesChanged += (_, _) => _ = RefreshAsync();
            return _context;
        }

        private void SetEdition(AppEdition edition)
        {
            if (_edition == edition) return;
            _edition = edition;
            EditionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
