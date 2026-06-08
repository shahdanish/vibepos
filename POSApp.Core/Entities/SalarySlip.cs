namespace POSApp.Core.Entities
{
    public sealed class SalarySlip
    {
        public int Id { get; set; }
        public string SlipNumber { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HouseRentAllowance { get; set; }
        public decimal MedicalAllowance { get; set; }
        public decimal OtherAllowances { get; set; }
        public decimal IncomeTax { get; set; }
        public decimal EobiDeduction { get; set; }
        public decimal OtherDeductions { get; set; }
        public decimal GrossSalary => BasicSalary + HouseRentAllowance + MedicalAllowance + OtherAllowances;
        public decimal TotalDeductions => IncomeTax + EobiDeduction + OtherDeductions;
        public decimal NetSalary => GrossSalary - TotalDeductions;
        public string? Notes { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.Now;
        public string? GeneratedByUsername { get; set; }
        public bool IsDeleted { get; set; } = false;

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    }
}
