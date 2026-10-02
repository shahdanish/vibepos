using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using POSApp.Core.Interfaces;

namespace POSApp.Infrastructure.Payments
{
    /// <summary>
    /// Stripe Terminal, server-driven: the program asks Stripe's API to show the amount on the
    /// shop's smart reader (e.g. a WisePOS E), the customer taps or inserts their card there,
    /// and the program reads back the result. Card data stays on the reader and at Stripe.
    /// Uses only HttpClient and Stripe's documented REST endpoints.
    /// </summary>
    public sealed class StripeTerminal : ICardTerminal
    {
        public const string ApiBase = "https://api.stripe.com/v1/";

        private readonly HttpClient _http;
        private readonly string _apiBase;
        private readonly string _readerId;
        private readonly TimeSpan _pollInterval;
        private readonly TimeSpan _timeout;
        private string? _pendingPaymentIntent;

        public StripeTerminal(HttpClient http, string apiKey, string readerId, TimeSpan? pollInterval = null, TimeSpan? timeout = null,
                              string? apiBase = null)
        {
            _http = http;
            _apiBase = apiBase ?? ApiBase;
            _readerId = readerId;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
            _timeout = timeout ?? TimeSpan.FromSeconds(120);
            IsTestMode = apiKey.StartsWith("sk_test_", StringComparison.Ordinal) || apiKey.StartsWith("rk_test_", StringComparison.Ordinal);
            _authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        private readonly AuthenticationHeaderValue _authorization;

        public bool IsConfigured => true;
        public bool IsTestMode { get; }

        public async Task<CardChargeResult> ChargeAsync(decimal amount, string description, CancellationToken ct = default)
        {
            if (amount <= 0) return CardChargeResult.Declined("Nothing to charge.");

            // 1. The payment, for exactly this amount in cents. Captured below once approved.
            var intent = await Post("payment_intents", new Dictionary<string, string>
            {
                ["amount"] = ToCents(amount).ToString(CultureInfo.InvariantCulture),
                ["currency"] = "usd",
                ["payment_method_types[]"] = "card_present",
                ["capture_method"] = "manual",
                ["description"] = description
            }, ct);
            var intentId = intent.GetProperty("id").GetString()!;
            _pendingPaymentIntent = intentId;

            try
            {
                // 2. Show it on the reader.
                await Post($"terminal/readers/{_readerId}/process_payment_intent",
                    new Dictionary<string, string> { ["payment_intent"] = intentId }, ct);

                // 3. Wait for the customer.
                var deadline = DateTime.UtcNow + _timeout;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var reader = await Get($"terminal/readers/{_readerId}", ct);
                    var (status, failure) = ReaderAction(reader);
                    if (status == "succeeded") break;
                    if (status == "failed")
                    {
                        await TryCancelIntent(intentId);
                        return CardChargeResult.Declined(failure ?? "The card was declined.", intentId);
                    }
                    if (DateTime.UtcNow > deadline)
                    {
                        await CancelAsync(CancellationToken.None);
                        return CardChargeResult.Declined("No card was presented in time.", intentId);
                    }
                    await Task.Delay(_pollInterval, ct);
                }

                // 4. Capture (if the account did not capture automatically) and read the card details.
                var paid = await Get($"payment_intents/{intentId}?expand[]=latest_charge", ct);
                if (Str(paid, "status") == "requires_capture")
                    paid = await Post($"payment_intents/{intentId}/capture", new Dictionary<string, string>(), ct, expandLatestCharge: true);
                if (Str(paid, "status") != "succeeded")
                {
                    // The reader finished but the payment is not captured. Release it so the
                    // card is not left with an open authorization.
                    await CancelAsync(CancellationToken.None);
                    return CardChargeResult.Declined($"The payment did not complete ({Str(paid, "status")}).", intentId);
                }

                var (brand, last4, auth) = CardDetails(paid);
                _pendingPaymentIntent = null;
                return new CardChargeResult(true, intentId, brand, last4, auth, null);
            }
            catch (OperationCanceledException)
            {
                await CancelAsync(CancellationToken.None);
                return CardChargeResult.Declined("Cancelled.", intentId);
            }
            catch (Exception)
            {
                // The payment exists at Stripe. Drop it before the error reaches the cashier,
                // so a retry starts clean instead of leaving an open charge.
                await CancelAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task CancelAsync(CancellationToken ct = default)
        {
            try { await Post($"terminal/readers/{_readerId}/cancel_action", new Dictionary<string, string>(), ct); }
            catch { /* nothing waiting on the reader */ }
            if (_pendingPaymentIntent is { } pending)
            {
                await TryCancelIntent(pending);
                _pendingPaymentIntent = null;
            }
        }

        public async Task<CardRefundResult> RefundAsync(string paymentReference, decimal amount, CancellationToken ct = default)
        {
            try
            {
                var refund = await Post("refunds", new Dictionary<string, string>
                {
                    ["payment_intent"] = paymentReference,
                    ["amount"] = ToCents(amount).ToString(CultureInfo.InvariantCulture)
                }, ct);
                var status = Str(refund, "status");
                return status is "succeeded" or "pending"
                    ? new CardRefundResult(true, Str(refund, "id"), status == "pending" ? "The refund is on its way to the card." : null)
                    : new CardRefundResult(false, Str(refund, "id"), $"The refund was not accepted ({status}).");
            }
            catch (StripeException ex)
            {
                return new CardRefundResult(false, null, ex.Message);
            }
        }

        public async Task SimulateCardAsync(CancellationToken ct = default)
        {
            if (!IsTestMode) throw new InvalidOperationException("Test cards work only with test keys.");
            await Post($"test_helpers/terminal/readers/{_readerId}/present_payment_method", new Dictionary<string, string>(), ct);
        }

        // ── Set-up helpers for Business Settings ────────────────────────────

        /// <summary>The account's readers: id, label, and whether it is online.</summary>
        public async Task<IReadOnlyList<(string Id, string Label, bool Online)>> ListReadersAsync(CancellationToken ct = default)
        {
            var list = await Get("terminal/readers?limit=100", ct);
            return list.GetProperty("data").EnumerateArray()
                .Select(r => (Str(r, "id") ?? "", Str(r, "label") ?? Str(r, "device_type") ?? "Reader", Str(r, "status") == "online"))
                .ToList();
        }

        /// <summary>Test mode: a location and a simulated reader, ready for test cards. Returns the reader id.</summary>
        public async Task<string> CreateSimulatedReaderAsync(CancellationToken ct = default)
        {
            if (!IsTestMode) throw new InvalidOperationException("A simulated reader needs test keys.");
            var location = await Post("terminal/locations", new Dictionary<string, string>
            {
                ["display_name"] = "Test location",
                ["address[line1]"] = "100 Main Street",
                ["address[city]"] = "Springfield",
                ["address[state]"] = "IL",
                ["address[postal_code]"] = "62701",
                ["address[country]"] = "US"
            }, ct);
            var reader = await Post("terminal/readers", new Dictionary<string, string>
            {
                ["registration_code"] = "simulated-wpe",
                ["location"] = Str(location, "id")!,
                ["label"] = "Simulated reader"
            }, ct);
            return Str(reader, "id")!;
        }

        // ── HTTP and JSON ───────────────────────────────────────────────────

        public static long ToCents(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

        private Task<JsonElement> Get(string path, CancellationToken ct) => Send(new HttpRequestMessage(HttpMethod.Get, _apiBase + path), ct);

        private Task<JsonElement> Post(string path, Dictionary<string, string> form, CancellationToken ct, bool expandLatestCharge = false)
        {
            if (expandLatestCharge) form["expand[]"] = "latest_charge";
            return Send(new HttpRequestMessage(HttpMethod.Post, _apiBase + path) { Content = new FormUrlEncodedContent(form) }, ct);
        }

        private async Task<JsonElement> Send(HttpRequestMessage request, CancellationToken ct)
        {
            request.Headers.Authorization = _authorization;
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            JsonElement json;
            try
            {
                using var document = JsonDocument.Parse(body);
                json = document.RootElement.Clone();
            }
            catch (JsonException) { throw new StripeException($"Unexpected reply from Stripe ({(int)response.StatusCode})."); }

            if (!response.IsSuccessStatusCode)
            {
                var message = json.TryGetProperty("error", out var error) ? Str(error, "message") : null;
                throw new StripeException(message ?? $"Stripe returned {(int)response.StatusCode}.");
            }
            return json;
        }

        private async Task TryCancelIntent(string intentId)
        {
            try { await Post($"payment_intents/{intentId}/cancel", new Dictionary<string, string>(), CancellationToken.None); }
            catch { /* already finished or cancelled */ }
        }

        private static (string? Status, string? Failure) ReaderAction(JsonElement reader) =>
            reader.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.Object
                ? (Str(action, "status"), Str(action, "failure_message"))
                : (null, null);

        private static (string? Brand, string? Last4, string? Auth) CardDetails(JsonElement intent)
        {
            if (!intent.TryGetProperty("latest_charge", out var charge) || charge.ValueKind != JsonValueKind.Object ||
                !charge.TryGetProperty("payment_method_details", out var details) ||
                !details.TryGetProperty("card_present", out var card))
                return (null, null, null);
            var auth = card.TryGetProperty("receipt", out var receipt) && receipt.ValueKind == JsonValueKind.Object
                ? Str(receipt, "authorization_code") : null;
            return (Brand(Str(card, "brand")), Str(card, "last4"), auth);
        }

        private static string? Brand(string? brand) => brand switch
        {
            "visa" => "Visa",
            "mastercard" => "Mastercard",
            "amex" => "American Express",
            "discover" => "Discover",
            "interac" => "Interac",
            null => null,
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(brand)
        };

        private static string? Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>An error Stripe reported (its message is safe to show the cashier).</summary>
    public sealed class StripeException : Exception
    {
        public StripeException(string message) : base(message) { }
    }
}
