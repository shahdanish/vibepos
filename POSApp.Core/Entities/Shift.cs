namespace POSApp.Core.Entities
{
    public sealed class Shift
    {
        public int Id { get; set; }
        public DateTime OpenedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ExpectedClosingBalance { get; set; }
        public decimal ActualClosingBalance { get; set; }
        public decimal Difference => ActualClosingBalance - ExpectedClosingBalance;
        public bool IsClosed => ClosedAt.HasValue;
    }
}
