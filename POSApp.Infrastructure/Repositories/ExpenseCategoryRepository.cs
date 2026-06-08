using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class ExpenseCategoryRepository : IExpenseCategoryRepository
    {
        private readonly AppDbContext _context;

        public ExpenseCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ExpenseCategory?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.ExpenseCategories.FindAsync([id], ct);
        }

        public async Task<IEnumerable<ExpenseCategory>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.ExpenseCategories
                .OrderBy(c => c.Name)
                .ToListAsync(ct);
        }

        public async Task<ExpenseCategory> AddAsync(ExpenseCategory category, CancellationToken ct = default)
        {
            _context.ExpenseCategories.Add(category);
            await _context.SaveChangesAsync(ct);
            return category;
        }

        public async Task UpdateAsync(ExpenseCategory category, CancellationToken ct = default)
        {
            category.ModifiedDate = DateTime.Now;
            _context.Entry(category).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var category = await _context.ExpenseCategories.FindAsync([id], ct);
            if (category != null)
            {
                _context.ExpenseCategories.Remove(category);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
