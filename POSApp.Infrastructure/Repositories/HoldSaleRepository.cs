using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class HoldSaleRepository : IHoldSaleRepository
    {
        private readonly AppDbContext _context;

        public HoldSaleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<HoldSale>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.HoldSales
                .Include(hs => hs.Items)
                .OrderByDescending(hs => hs.HeldAt)
                .ToListAsync(ct);
        }

        public async Task<HoldSale?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.HoldSales
                .Include(hs => hs.Items)
                .FirstOrDefaultAsync(hs => hs.Id == id, ct);
        }

        public async Task<HoldSale> AddAsync(HoldSale holdSale, CancellationToken ct = default)
        {
            _context.HoldSales.Add(holdSale);
            await _context.SaveChangesAsync(ct);
            return holdSale;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var holdSale = await _context.HoldSales
                .Include(hs => hs.Items)
                .FirstOrDefaultAsync(hs => hs.Id == id, ct);
            if (holdSale != null)
            {
                _context.HoldSales.Remove(holdSale);
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
