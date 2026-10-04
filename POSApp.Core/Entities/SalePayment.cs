namespace POSApp.Core.Entities
{
    /// <summary>
    /// One tender on a US sale: cash, card, check or the customer's charge account. A split
    /// payment has several rows. Pakistani sales (and every sale made before tenders existed)
    /// have none and keep using <see cref="Sale.PaymentType"/> and <see cref="Sale.ReceiveCash"/>.
    /// </summary>
    public sealed class SalePayment
    {
        public int Id { get; set; }
        public int SaleId { get; set; }

        /// <summary>See <see cref="PaymentMethods"/>.</summary>
        public string Method { get; set; } = PaymentMethods.Cash;

        /// <summary>The amount applied to the sale. Negative on a refund.</summary>
        public decimal Amount { get; set; }

        /// <summary>What the customer handed over. Only cash can exceed <see cref="Amount"/>; the difference is change.</summary>
        public decimal Tendered { get; set; }

        /// <summary>Card network as the terminal printed it, e.g. "Visa". Never the full card number.</summary>
        public string? CardBrand { get; set; }

        /// <summary>Last four digits of the card, for the receipt and look-ups.</summary>
        public string? CardLast4 { get; set; }

        /// <summary>Card authorization code or check number.</summary>
        public string? Reference { get; set; }

        /// <summary>The payment provider's id for an integrated card payment (used to refund it).</summary>
        public string? ProcessorReference { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public Sale? Sale { get; set; }

        /// <summary>Cash handed back for this tender.</summary>
        public decimal Change => Method == PaymentMethods.Cash && Tendered > Amount ? Tendered - Amount : 0;
    }

    /// <summary>The tender types a US sale can take.</summary>
    public static class PaymentMethods
    {
        public const string Cash = "Cash";
        public const string Card = "Card";
        public const string Check = "Check";
        public const string ChargeAccount = "Charge Account";

        /// <summary>Store gift card. The code is in <see cref="SalePayment.Reference"/>.</summary>
        public const string GiftCard = "Gift Card";

        /// <summary>Loyalty points. The points used are in <see cref="SalePayment.Reference"/>.</summary>
        public const string Loyalty = "Loyalty";

        /// <summary>Recorded as <see cref="Sale.PaymentType"/> when a sale was paid with more than one tender.</summary>
        public const string Split = "Split";

        public static IReadOnlyList<string> UnitedStates { get; } = new[] { Cash, Card, Check, ChargeAccount };

        /// <summary>The original (Pakistani) payment list, unchanged.</summary>
        public static IReadOnlyList<string> Original { get; } = new[] { "Cash", "Credit", "Credit Card", "Bank Transfer" };

        /// <summary>Tenders that put the amount on the customer's account (owed by the customer).</summary>
        public static bool IsOnAccount(string? method) => method is "Credit" or ChargeAccount;
    }
}
