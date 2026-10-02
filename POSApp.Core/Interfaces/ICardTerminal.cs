namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// An integrated card reader (US). The card is read by the reader and processed by the
    /// shop's payment provider; the program never sees or stores the card number — only the
    /// approval, card brand, last four digits and the provider's reference for refunds.
    /// </summary>
    public interface ICardTerminal
    {
        /// <summary>A reader is set up for this shop.</summary>
        bool IsConfigured { get; }

        /// <summary>The provider's test mode: no real money moves.</summary>
        bool IsTestMode { get; }

        /// <summary>Sends the amount to the reader and waits for the customer's card (or a cancel).</summary>
        Task<CardChargeResult> ChargeAsync(decimal amount, string description, CancellationToken ct = default);

        /// <summary>Stops a payment that is waiting on the reader.</summary>
        Task CancelAsync(CancellationToken ct = default);

        /// <summary>Refunds part or all of an earlier card payment.</summary>
        Task<CardRefundResult> RefundAsync(string paymentReference, decimal amount, CancellationToken ct = default);

        /// <summary>Test mode only: presents a test card on the simulated reader.</summary>
        Task SimulateCardAsync(CancellationToken ct = default);
    }

    public sealed record CardChargeResult(bool Approved, string? PaymentReference, string? Brand, string? Last4, string? AuthCode, string? Message)
    {
        public static CardChargeResult Declined(string message, string? reference = null) => new(false, reference, null, null, null, message);
    }

    public sealed record CardRefundResult(bool Succeeded, string? RefundReference, string? Message);

    /// <summary>The shop's card reader set-up (the secret key is stored encrypted on this PC).</summary>
    public sealed record CardTerminalSettings(string Provider, string? ApiKey, string? ReaderId)
    {
        public const string None = "None";
        public const string StripeTerminal = "StripeTerminal";

        public static CardTerminalSettings Off { get; } = new(None, null, null);

        public bool IsStripe => Provider == StripeTerminal && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ReaderId);

        public bool IsTestKey => ApiKey is { } k && (k.StartsWith("sk_test_", StringComparison.Ordinal) || k.StartsWith("rk_test_", StringComparison.Ordinal));
    }

    /// <summary>Reads and saves <see cref="CardTerminalSettings"/>.</summary>
    public interface ICardTerminalSettingsStore
    {
        Task<CardTerminalSettings> GetAsync(CancellationToken ct = default);
        Task SaveAsync(CardTerminalSettings settings, CancellationToken ct = default);
    }
}
