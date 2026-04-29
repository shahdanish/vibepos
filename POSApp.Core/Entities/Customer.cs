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
        public decimal TotalPurchases { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
        
        public List<CustomerPayment> Payments { get; set; } = new();
    }
}
