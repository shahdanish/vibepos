namespace POSApp.Core.Entities
{
    public sealed class Doctor
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Specialization { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? PmdcLicenseNo { get; set; }
        public string DisplayLabel => string.IsNullOrWhiteSpace(Specialization) ? Name : $"{Name} — {Specialization}";
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}
