using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IExpenseCategoryRepository
    {
        Task<ExpenseCategory?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<ExpenseCategory>> GetAllAsync(CancellationToken ct = default);
        Task<ExpenseCategory> AddAsync(ExpenseCategory category, CancellationToken ct = default);
        Task UpdateAsync(ExpenseCategory category, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
