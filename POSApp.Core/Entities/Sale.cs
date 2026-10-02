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

        /// <summary>Sales tax included in <see cref="TotalBill"/> (US). 0 on Pakistani and older sales.</summary>
        public decimal TaxTotal { get; set; }

        /// <summary>The customer's exemption certificate number when the sale was tax-exempt.</summary>
        public string? TaxExemptNumber { get; set; }
        public decimal ReceiveCash { get; set; }
        public decimal Balance { get; set; }
        public bool AutoPrinted { get; set; }
        public int? PharmacyId { get; set; }
        public int? DoctorId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation properties
        public Customer? Customer { get; set; }
        public Pharmacy? Pharmacy { get; set; }
        public Doctor? Doctor { get; set; }
        public List<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

        /// <summary>Tenders (US sales). Empty on Pakistani and older sales.</summary>
        public List<SalePayment> Payments { get; set; } = new List<SalePayment>();
    }
}
