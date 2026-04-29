using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class PurchaseRepository : IPurchaseRepository
    {
        private readonly AppDbContext _context;

        public PurchaseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PurchaseOrder?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<PurchaseOrder?> GetByPurchaseNumberAsync(string purchaseNumber, CancellationToken ct = default)
        {
            return await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.PurchaseNumber == purchaseNumber, ct);
        }

        public async Task<IEnumerable<PurchaseOrder>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.PurchaseOrders
                .Include(p => p.Items)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<PurchaseOrder>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            return await _context.PurchaseOrders
                .Include(p => p.Items)
                .Where(p => p.PurchaseDate >= startDate && p.PurchaseDate <= endDate)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync(ct);
        }

        public async Task<PurchaseOrder> AddAsync(PurchaseOrder purchase, CancellationToken ct = default)
        {
            _context.PurchaseOrders.Add(purchase);
            await _context.SaveChangesAsync(ct);
            return purchase;
        }

        public async Task UpdateAsync(PurchaseOrder purchase, CancellationToken ct = default)
        {
            _context.PurchaseOrders.Update(purchase);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<string> GetNextPurchaseNumberAsync(CancellationToken ct = default)
        {
            var lastPurchase = await _context.PurchaseOrders
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (lastPurchase == null)
                return "PUR-0001";

            var lastNumber = lastPurchase.PurchaseNumber.Replace("PUR-", "");
            if (int.TryParse(lastNumber, out int number))
            {
                return $"PUR-{(number + 1):D4}";
            }

            return "PUR-0001";
        }
    }
}
