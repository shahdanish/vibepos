namespace POSApp.Core.Entities
{
    public sealed class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public int Bonus { get; set; }
        public decimal CostPrice { get; set; } // Cost price for profit calculation
        public decimal UnitPrice { get; set; } // Selling price
        public decimal DiscountPercent { get; set; }
        public string DiscountType { get; set; } = "%"; // "%" (percentage) or "AMT" (flat amount; legacy rows store "PKR")
        public decimal Total { get; set; }

        /// <summary>Sales-tax rate applied to this line, in percent (0 when untaxed).</summary>
        public decimal TaxRate { get; set; }

        /// <summary>Sales tax charged on this line, after its share of any bill discount.</summary>
        public decimal TaxAmount { get; set; }

        // Navigation property
        public Sale? Sale { get; set; }
        public Product? Product { get; set; } // Navigation property for product details
    }
}
