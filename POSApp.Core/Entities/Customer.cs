namespace POSApp.Core.Entities
{
    public sealed class Customer
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? CellNo { get; set; }
        public string? Address { get; set; }
        public decimal PreBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public int LoyaltyPoints { get; set; }

        /// <summary>When set, this customer's purchases earn loyalty points (US).</summary>
        public bool LoyaltyEnrolled { get; set; }
        public decimal TotalPurchases { get; set; }
        public DateTime? LastPurchaseDate { get; set; }

        /// <summary>Sales to this customer carry no sales tax (US resale or exempt organisation).</summary>
        public bool IsTaxExempt { get; set; }

        /// <summary>The customer's exemption certificate number, printed on exempt receipts.</summary>
        public string? TaxExemptNumber { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
        
        public List<CustomerPayment> Payments { get; set; } = new();
    }
}
