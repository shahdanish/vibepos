namespace POSApp.Core.Entities
{
    public sealed class Sale
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string SaleType { get; set; } = "Sale"; // "Sale", "WholeSale", or "Return"
        public string PaymentType { get; set; } = "Cash";
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = "Cash";
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? MobileNumber { get; set; } // Customer mobile number
        public decimal PreBalance { get; set; }
        public string? BillNote { get; set; }
        public decimal DiscountOnProducts { get; set; }
        public decimal DiscountOnBill { get; set; }
        public decimal TotalBill { get; set; }
        public decimal ReceiveCash { get; set; }
        public decimal Balance { get; set; }
        public bool AutoPrinted { get; set; } // Track if auto-printed
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation property
        public Customer? Customer { get; set; }
        public List<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
