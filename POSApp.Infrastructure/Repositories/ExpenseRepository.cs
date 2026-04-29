using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class ExpenseRepository : IExpenseRepository
    {
        private readonly AppDbContext _context;

        public ExpenseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Expense?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Expenses.FindAsync([id], ct);
        }

        public async Task<IEnumerable<Expense>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Expenses
                .OrderByDescending(e => e.Date)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Expense>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
        {
            return await _context.Expenses
                .Where(e => e.Date >= start.Date && e.Date < end.Date.AddDays(1))
                .OrderByDescending(e => e.Date)
                .ToListAsync(ct);
        }

        public async Task<decimal> GetTotalByDateAsync(DateTime date, CancellationToken ct = default)
        {
            return await _context.Expenses
                .Where(e => e.Date >= date.Date && e.Date < date.Date.AddDays(1))
                .SumAsync(e => e.Amount, ct);
        }

        public async Task<Expense> AddAsync(Expense expense, CancellationToken ct = default)
        {
            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync(ct);
            return expense;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var expense = await _context.Expenses.FindAsync([id], ct);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
