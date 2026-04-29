using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IExpenseRepository
    {
        Task<Expense?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Expense>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<Expense>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);
        Task<decimal> GetTotalByDateAsync(DateTime date, CancellationToken ct = default);
        Task<Expense> AddAsync(Expense expense, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
