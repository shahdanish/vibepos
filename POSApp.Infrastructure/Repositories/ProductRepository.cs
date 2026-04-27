using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products.FindAsync(id);
        }

        public async Task<Product?> GetByProductIdAsync(string productId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => (p.ProductId == productId || p.Barcode == productId) && !p.IsDeleted);
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Barcode == barcode && !p.IsDeleted);
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            // Global query filter already excludes deleted products
            return await _context.Products
                .Include(p => p.Category)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetAllIncludingDeletedAsync()
        {
            // Use IgnoreQueryFilters to bypass the soft delete filter
            return await _context.Products
                .IgnoreQueryFilters()
                .Include(p => p.Category)
                .ToListAsync();
        }

        public async Task<Product> AddAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task UpdateAsync(Product product)
        {
            product.ModifiedDate = DateTime.Now;
            _context.Entry(product).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Soft delete: set IsDeleted flag instead of removing
                product.IsDeleted = true;
                product.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Stock <= p.MinStockThreshold && !p.IsDeleted)
                .OrderBy(p => p.Stock)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetSlowSellingProductsAsync(int daysToConsider = 30, int minSalesCount = 5)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToConsider);
            
            // Get products with low or no sales in the specified period
            var slowSellers = await _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .Select(p => new { Product = p, SalesCount = _context.SaleItems
                    .Where(si => si.ProductId == p.ProductId && si.Sale!.SaleDate >= cutoffDate)
                    .Count() })
                .Where(x => x.SalesCount < minSalesCount)
                .OrderBy(x => x.SalesCount)
                .ThenByDescending(x => x.SalesCount) // Products with 0 sales first
                .Select(x => x.Product)
                .ToListAsync();
            
            return slowSellers;
        }

        public async Task<IEnumerable<Product>> SearchAsync(string searchTerm)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.ProductId.Contains(searchTerm) || 
                           p.Barcode.Contains(searchTerm) ||
                           p.ProductName.Contains(searchTerm))
                .ToListAsync();
        }
    }
}
