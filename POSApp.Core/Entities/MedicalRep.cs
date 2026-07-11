namespace POSApp.Core.Entities
{
    /// <summary>
    /// A medical representative — the person who makes the scheduled call on a Doctor.
    /// Master data, mirrors the Doctor entity's soft-delete/active conventions.
    /// </summary>
    public sealed class MedicalRep
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Phone { get; set; }
        public string DisplayLabel => string.IsNullOrWhiteSpace(Company) ? Name : $"{Name} — {Company}";
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
    }
}
