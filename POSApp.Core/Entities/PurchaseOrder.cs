namespace POSApp.Core.Entities
{
    public sealed class PurchaseOrder
    {
        public int Id { get; set; }
        public string PurchaseNumber { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; } = DateTime.Now;
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        // Navigation property
        public List<PurchaseOrderItem> Items { get; set; } = new();
        public Supplier? Supplier { get; set; }
    }
}
