namespace POSApp.Core.Entities
{
    /// <summary>
    /// A category used to classify expenses (e.g. Rent, Utilities, Salaries).
    /// Independent from the product/inventory <see cref="Category"/>.
    /// </summary>
    public sealed class ExpenseCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }

        // Navigation property
        public List<Expense> Expenses { get; set; } = new();
    }
}
