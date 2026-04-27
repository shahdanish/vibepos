namespace POSApp.Core.Entities
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal CostPrice { get; set; } // Cost price for profit calculation
        public decimal UnitPrice { get; set; } // Selling price
        public decimal DiscountPercent { get; set; }
        public decimal Total { get; set; }
        
        // Navigation property
        public Sale? Sale { get; set; }
        public Product? Product { get; set; } // Navigation property for product details
    }
}
