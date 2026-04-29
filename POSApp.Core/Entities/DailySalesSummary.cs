namespace POSApp.Core.Entities
{
    public sealed class DailySalesSummary
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal ExpectedClosing { get; set; }
        public decimal ActualClosing { get; set; }
        public decimal Variance { get; set; }
        public string? Notes { get; set; }
        public int? ShiftId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
