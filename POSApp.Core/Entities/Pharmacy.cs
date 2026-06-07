namespace POSApp.Core.Entities
{
    public sealed class Pharmacy
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Area { get; set; }
        public string? Address { get; set; }
        public string? LicenseNo { get; set; }
        public string? Ntn { get; set; }
        public string DisplayLabel => string.IsNullOrWhiteSpace(Area) ? Name : $"{Name} — {Area}";
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}
