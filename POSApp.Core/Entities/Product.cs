namespace POSApp.Core.Entities
{
    public sealed class Product
    {
        public int Id { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; } // Purchase/Cost price for profit calculation
        public decimal UnitPrice { get; set; } // Retail selling price
        public decimal WholesalePrice { get; set; } // Wholesale price
        public int Stock { get; set; }
        public int MinStockThreshold { get; set; } = 10; // Default threshold for low stock alert
        public decimal ProfitMarginPercentage { get; set; } = 200; // Default 200% profit margin
        public bool IsDeleted { get; set; } = false; // Soft delete flag
        public string? Rack { get; set; }
        public int? CategoryId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }

        // Navigation property
        public Category? Category { get; set; }
    }
}
