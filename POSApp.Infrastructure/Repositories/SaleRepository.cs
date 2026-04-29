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
        
        public async Task<IEnumerable<SalesByCategoryDto>> GetSalesByCategoryAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            var start = startDate.Date;
            var end = endDate.Date.AddDays(1);
            
            return await _context.SaleItems
                .Include(si => si.Product)
                .ThenInclude(p => p!.Category)
                .Where(si => si.Sale!.SaleDate >= start && si.Sale!.SaleDate < end)
                .GroupBy(si => si.Product!.Category != null ? si.Product.Category.Name : "Uncategorized")
                .Select(g => new SalesByCategoryDto
                {
                    CategoryName = g.Key,
                    TotalQuantity = g.Sum(si => si.Quantity),
                    TotalSales = g.Sum(si => si.Total),
                    TotalProfit = g.Sum(si => (si.UnitPrice - si.CostPrice) * si.Quantity)
                })
                .OrderByDescending(x => x.TotalSales)
                .ToListAsync(ct);
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
