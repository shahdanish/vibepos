namespace POSApp.Core.Entities
{
    public sealed class HoldSaleItem
    {
        public int Id { get; set; }
        public int HoldSaleId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        
        public HoldSale? HoldSale { get; set; }
    }
}
