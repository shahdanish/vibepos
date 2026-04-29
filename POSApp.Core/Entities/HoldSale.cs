namespace POSApp.Core.Entities
{
    public sealed class HoldSale
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public DateTime HeldAt { get; set; } = DateTime.Now;
        public string? CustomerName { get; set; }
        public decimal TotalBill { get; set; }
        public List<HoldSaleItem> Items { get; set; } = new();
    }
}
