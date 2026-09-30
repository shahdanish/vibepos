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

        public static bool IsProProductId(string? productId) =>
            !string.IsNullOrEmpty(productId) &&
            productId.StartsWith(ProProductIdPrefix, StringComparison.OrdinalIgnoreCase);

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

        public async Task<IReadOnlyList<ProOffer>> GetProOffersAsync()
        {
            if (!_isStoreBuild) return Array.Empty<ProOffer>();

#if DEBUG
            if (_debugForced)
            {
                return new[]
                {
                    new ProOffer("debug-monthly", "Swifttill Pro — Monthly", "$1.99/month", "1 month free trial"),
                    new ProOffer("debug-yearly",  "Swifttill Pro — Yearly",  "$19.99/year", null)
                };
            }
#endif
            if (!AppEnvironment.IsPackaged) return Array.Empty<ProOffer>();

            try
            {
                // Subscription add-ons are returned under the "Durable" product kind.
                var query = await GetContext().GetAssociatedStoreProductsAsync(new[] { "Durable" });
                _products.Clear();

                var offers = new List<ProOffer>();
                foreach (var product in query.Products.Values.Where(p => IsProProductId(p.InAppOfferToken)))
                {
                    _products[product.StoreId] = product;

                    var paid = product.Skus.FirstOrDefault(s => s.IsSubscription && !s.IsTrial) ?? product.Skus.FirstOrDefault();
                    var trial = product.Skus.FirstOrDefault(s => s.IsTrial);
                    var price = paid != null ? FormatPrice(paid) : product.Price.FormattedPrice;
                    var trialText = trial?.SubscriptionInfo is { } t && t.HasTrialPeriod
                        ? $"{t.TrialPeriod} {Unit(t.TrialPeriodUnit, t.TrialPeriod)} free trial"
                        : null;

                    offers.Add(new ProOffer(product.StoreId, product.Title, price, trialText));
                }

                // Cheapest-per-period plans first is not knowable without FX maths; keep monthly before yearly.
                return offers
                    .OrderBy(o => o.Title.Contains("year", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ToList();
            }
            catch
            {
                return Array.Empty<ProOffer>();
            }
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

                if (_products.Count == 0) await GetProOffersAsync();
                if (!_products.TryGetValue(offerId, out var product)) return UpgradeResult.NotAvailable;

                // Offer the free-trial SKU to people who haven't used it yet; the Store handles
                // eligibility and falls back to the paid SKU in its own dialog.
                var trialSku = product.Skus.FirstOrDefault(s => s.IsTrial && !s.IsInUserCollection);
                var result = trialSku != null
                    ? await trialSku.RequestPurchaseAsync()
                    : await product.RequestPurchaseAsync();

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
            catch
            {
                return UpgradeResult.Failed;
            }
        }

        private static string FormatPrice(StoreSku sku)
        {
            var info = sku.SubscriptionInfo;
            var price = !string.IsNullOrEmpty(sku.Price.FormattedRecurrencePrice)
                ? sku.Price.FormattedRecurrencePrice
                : sku.Price.FormattedPrice;
            if (info == null) return price;

            var period = info.BillingPeriod == 1
                ? Unit(info.BillingPeriodUnit, 1)
                : $"{info.BillingPeriod} {Unit(info.BillingPeriodUnit, info.BillingPeriod)}";
            return $"{price}/{period}";
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
