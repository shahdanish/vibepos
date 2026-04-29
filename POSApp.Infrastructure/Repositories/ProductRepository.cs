using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Products.FindAsync([id], ct);
        }

        public async Task<Product?> GetByProductIdAsync(string productId, CancellationToken ct = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.ProductId == productId || p.Barcode == productId, ct);
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Barcode == barcode && !p.IsDeleted, ct);
        }

        public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken ct = default)
        {
            // Global query filter already excludes deleted products
            return await _context.Products
                .Include(p => p.Category)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Product>> GetAllIncludingDeletedAsync(CancellationToken ct = default)
        {
            // Use IgnoreQueryFilters to bypass the soft delete filter
            return await _context.Products
                .IgnoreQueryFilters()
                .Include(p => p.Category)
                .ToListAsync(ct);
        }

        public async Task<Product> AddAsync(Product product, CancellationToken ct = default)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync(ct);
            return product;
        }

        public async Task UpdateAsync(Product product, CancellationToken ct = default)
        {
            product.ModifiedDate = DateTime.Now;
            _context.Entry(product).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var product = await _context.Products.FindAsync([id], ct);
            if (product != null)
            {
                // Soft delete: set IsDeleted flag instead of removing
                product.IsDeleted = true;
                product.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(CancellationToken ct = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock <= p.MinStockThreshold)
                .OrderBy(p => p.Stock)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Product>> GetSlowSellingProductsAsync(int daysToConsider = 30, int minSalesCount = 5, CancellationToken ct = default)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToConsider);

            // Get products with low or no sales in the specified period
            var slowSellers = await _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .Select(p => new
                {
                    Product = p,
                    SalesCount = _context.SaleItems
                    .Where(si => si.ProductId == p.ProductId && si.Sale!.SaleDate >= cutoffDate)
                    .Count()
                })
                .Where(x => x.SalesCount < minSalesCount)
                .OrderBy(x => x.SalesCount)
                .Select(x => x.Product)
                .ToListAsync(ct);

            return slowSellers;
        }

        public async Task<IEnumerable<Product>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.ProductId.Contains(searchTerm) ||
                           p.Barcode.Contains(searchTerm) ||
                           p.ProductName.Contains(searchTerm))
                .ToListAsync(ct);
        }
    }
}
