using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _context;

        public CategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Categories
                .Include(c => c.Products)
                .ToListAsync(ct);
        }

        public async Task<Category> AddAsync(Category category, CancellationToken ct = default)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync(ct);
            return category;
        }

        public async Task UpdateAsync(Category category, CancellationToken ct = default)
        {
            category.ModifiedDate = DateTime.Now;
            _context.Entry(category).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var category = await _context.Categories.FindAsync([id], ct);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<IEnumerable<Category>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            return await _context.Categories
                .Where(c => c.Name.Contains(searchTerm) ||
                           (c.Description != null && c.Description.Contains(searchTerm)))
                .ToListAsync(ct);
        }
    }
}
