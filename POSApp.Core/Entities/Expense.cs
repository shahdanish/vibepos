namespace POSApp.Core.Entities
{
    public sealed class Expense
    {
        public int Id { get; set; }
        public required string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string? Note { get; set; }
    }
}
