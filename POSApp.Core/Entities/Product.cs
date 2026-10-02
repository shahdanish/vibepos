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
        public string? BatchNo { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? CategoryId { get; set; }

        /// <summary>Sales-tax class (US). Null means the shop's default tax category.</summary>
        public int? TaxCategoryId { get; set; }

        /// <summary>Customers must show ID proving this age (e.g. 18 or 21); 0 = no age check.</summary>
        public int MinimumAge { get; set; }

        /// <summary>Contains pseudoephedrine or ephedrine: each sale is written to the logbook and limited.</summary>
        public bool IsPse { get; set; }

        /// <summary>Base drug per package in mg, for the PSE limits (e.g. 24 × 30 mg HCl ≈ 590 mg base).</summary>
        public decimal PseBaseMgPerPack { get; set; }

        /// <summary>Can be paid for with an FSA/HSA card (the shop decides which items qualify).</summary>
        public bool IsFsaEligible { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }

        // Navigation property
        public Category? Category { get; set; }
    }
}
