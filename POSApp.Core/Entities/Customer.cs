namespace POSApp.Core.Entities
{
    public class Customer
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? CellNo { get; set; }
        public string? Address { get; set; }
        public decimal PreBalance { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}
