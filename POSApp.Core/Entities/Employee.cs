namespace POSApp.Core.Entities
{
    public sealed class Employee
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? FatherName { get; set; }
        public string? Cnic { get; set; }
        public string? CellNumber { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Department { get; set; }
        public DateTime JoiningDate { get; set; } = DateTime.Today;
        public decimal BasicSalary { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }

        public ICollection<SalarySlip> SalarySlips { get; set; } = new List<SalarySlip>();
    }
}
