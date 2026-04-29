namespace POSApp.Core.Entities
{
    public sealed class Supplier
    {
        public int Id { get; set; }
        public string SupplierId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public decimal CurrentBalance { get; set; }
        public string? PaymentTerms { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}
