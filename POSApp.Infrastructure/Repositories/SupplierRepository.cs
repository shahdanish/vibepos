using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class SupplierRepository : ISupplierRepository
    {
        private readonly AppDbContext _context;

        public SupplierRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Supplier?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Suppliers.FindAsync(new object[] { id }, ct);
        }

        public async Task<Supplier?> GetBySupplierIdAsync(string supplierId, CancellationToken ct = default)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct);
        }

        public async Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Suppliers
                .OrderBy(s => s.Name)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Supplier>> GetActiveAsync(CancellationToken ct = default)
        {
            return await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync(ct);
        }

        public async Task<Supplier> AddAsync(Supplier supplier, CancellationToken ct = default)
        {
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync(ct);
            return supplier;
        }

        public async Task UpdateAsync(Supplier supplier, CancellationToken ct = default)
        {
            supplier.ModifiedDate = DateTime.Now;
            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var supplier = await _context.Suppliers.FindAsync(new object[] { id }, ct);
            if (supplier != null)
            {
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<IEnumerable<Supplier>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            return await _context.Suppliers
                .Where(s => s.Name.Contains(searchTerm) || 
                           s.SupplierId.Contains(searchTerm) ||
                           (s.Phone != null && s.Phone.Contains(searchTerm)))
                .OrderBy(s => s.Name)
                .ToListAsync(ct);
        }
    }
}
