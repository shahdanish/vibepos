namespace POSApp.Core.Entities
{
    public sealed class SyncLog
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Product", "Sale", "SaleItem", etc.
        public int EntityId { get; set; }
        public string Operation { get; set; } = string.Empty;  // "Create", "Update", "Delete"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? SyncedAt { get; set; }               // null = pending sync
    }
}
