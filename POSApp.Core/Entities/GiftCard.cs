namespace POSApp.Core.Entities
{
    /// <summary>
    /// Store gift card. <see cref="Code"/> is what the cashier types. <see cref="Balance"/> is
    /// what is left to spend. The card number of a bank card is never stored here.
    /// </summary>
    public sealed class GiftCard
    {
        public int Id { get; set; }

        /// <summary>Printed on the receipt, e.g. GC4K7M2Q9P.</summary>
        public string Code { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        /// <summary>The amount the card was sold for.</summary>
        public decimal IssuedAmount { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.Now;

        public string? RecipientName { get; set; }

        /// <summary>A voided card cannot be spent.</summary>
        public bool IsVoid { get; set; }
    }
}
