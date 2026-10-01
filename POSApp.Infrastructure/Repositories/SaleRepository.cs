using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;
using System.Globalization;

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
                .Include(s => s.Pharmacy)
                .Include(s => s.Doctor)
                .FirstOrDefaultAsync(s => s.InvoiceNumber == invoiceNumber, ct);
        }

        public async Task<IEnumerable<Sale>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .Include(s => s.Pharmacy)
                .Include(s => s.Doctor)
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
                .Include(s => s.Pharmacy)
                .Include(s => s.Doctor)
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
                .Include(s => s.Pharmacy)
                .Include(s => s.Doctor)
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

        /// <summary>The number handed out when no numeric invoice exists yet.</summary>
        private const long FirstInvoiceNumber = 11016;

        public async Task<string> GetNextInvoiceNumberAsync(CancellationToken ct = default)
        {
            // The highest purely numeric invoice number, by value. Taking the last row by Id and
            // parsing it broke whenever that row was a return ("R-…"): the sequence restarted at
            // 11016 and handed out numbers that were already on customers' receipts.
            var highest = await _context.Database
                .SqlQueryRaw<long?>(
                    "SELECT MAX(CAST(InvoiceNumber AS INTEGER)) AS \"Value\" FROM Sales " +
                    "WHERE InvoiceNumber <> '' AND InvoiceNumber NOT GLOB '*[^0-9]*'")
                .SingleAsync(ct);

            return (highest is long n ? n + 1 : FirstInvoiceNumber).ToString(CultureInfo.InvariantCulture);
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
        
        public async Task<IEnumerable<SalesByCategoryDto>> GetSalesByCategoryAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1);
            
            var lines = await _context.SaleItems
                .Where(si => si.Sale!.SaleDate >= start && si.Sale!.SaleDate < end)
                .Select(si => new { si.ProductId, si.Quantity, si.Total, si.UnitPrice, si.CostPrice })
                .ToListAsync(ct);

            // SaleItem.ProductId holds the product CODE (Products.ProductId), not the key, so the
            // SaleItem.Product navigation is never populated (EF maps it to an unused shadow
            // column). Resolve the category by code instead; deleted products still count.
            var categoryByCode = (await _context.Products
                    .IgnoreQueryFilters()
                    .Select(p => new { p.ProductId, Category = p.Category != null ? p.Category.Name : null })
                    .ToListAsync(ct))
                .GroupBy(p => p.ProductId)
                .ToDictionary(g => g.Key, g => g.First().Category);

            return lines
                .GroupBy(l => categoryByCode.TryGetValue(l.ProductId, out var name) && name != null ? name : "Uncategorized")
                .Select(g => new SalesByCategoryDto
                {
                    CategoryName = g.Key,
                    TotalQuantity = g.Sum(l => l.Quantity),
                    TotalSales = g.Sum(l => l.Total),
                    TotalProfit = g.Sum(l => (l.UnitPrice - l.CostPrice) * l.Quantity)
                })
                .OrderByDescending(x => x.TotalSales)
                .ToList();
        }
        
        public async Task<IEnumerable<TopProductDto>> GetTopSellingProductsAsync(int count, DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1);
            
            return await _context.SaleItems
                .Where(si => si.Sale!.SaleDate >= start && si.Sale!.SaleDate < end)
                .GroupBy(si => new { si.ProductId, si.ProductName })
                .Select(g => new TopProductDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    TotalQuantity = g.Sum(si => si.Quantity),
                    TotalSales = g.Sum(si => si.Total),
                    TotalProfit = g.Sum(si => (si.UnitPrice - si.CostPrice) * si.Quantity)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(count)
                .ToListAsync(ct);
        }
        
        public async Task<decimal> GetTotalProfitAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1);
            
            var profit = await _context.SaleItems
                .Where(si => si.Sale!.SaleDate >= start && si.Sale!.SaleDate < end)
                .SumAsync(si => (si.UnitPrice - si.CostPrice) * si.Quantity, ct);
            
            return profit;
        }
        
        public async Task<IEnumerable<SalesByPaymentTypeDto>> GetSalesByPaymentTypeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1);
            
            return await _context.Sales
                .Where(s => s.SaleDate >= start && s.SaleDate < end)
                .GroupBy(s => s.PaymentType)
                .Select(g => new SalesByPaymentTypeDto
                {
                    PaymentType = g.Key,
                    TotalTransactions = g.Count(),
                    TotalAmount = g.Sum(s => s.TotalBill)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync(ct);
        }
    }
}
