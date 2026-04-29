using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class SaleRepository : ISaleRepository
    {
        private readonly AppDbContext _context;

        public SaleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Sale?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.Id == id, ct);
        }

        public async Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default)
        {
            return await _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .FirstOrDefaultAsync(s => s.InvoiceNumber == invoiceNumber, ct);
        }

        public async Task<IEnumerable<Sale>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Sale>> GetByDateAsync(DateTime date, CancellationToken ct = default)
        {
            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            return await _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .Where(s => s.SaleDate >= startDate && s.SaleDate < endDate)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1);

            return await _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .Where(s => s.SaleDate >= start && s.SaleDate < end)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync(ct);
        }

        public async Task<Sale> AddAsync(Sale sale, CancellationToken ct = default)
        {
            _context.Sales.Add(sale);
            await _context.SaveChangesAsync(ct);
            return sale;
        }

        public async Task UpdateAsync(Sale sale, CancellationToken ct = default)
        {
            _context.Entry(sale).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var sale = await _context.Sales.FindAsync([id], ct);
            if (sale != null)
            {
                _context.Sales.Remove(sale);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<string> GetNextInvoiceNumberAsync(CancellationToken ct = default)
        {
            var lastSale = await _context.Sales
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (lastSale == null)
            {
                return "11016";
            }

            if (int.TryParse(lastSale.InvoiceNumber, out int lastNumber))
            {
                return (lastNumber + 1).ToString();
            }

            return "11016";
        }

        public async Task<IEnumerable<SaleItem>> GetRecentSalesItemsAsync(int days = 30, CancellationToken ct = default)
        {
            var cutoffDate = DateTime.Now.AddDays(-days);

            return await _context.SaleItems
                .Include(si => si.Sale)
                .Include(si => si.Product)
                .Where(si => si.Sale!.SaleDate >= cutoffDate)
                .ToListAsync(ct);
        }
    }
}
