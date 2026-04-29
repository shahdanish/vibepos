using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Product?> GetByProductIdAsync(string productId, CancellationToken ct = default);
        Task<IEnumerable<Product>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<Product>> GetAllIncludingDeletedAsync(CancellationToken ct = default);
        Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
        Task<Product> AddAsync(Product product, CancellationToken ct = default);
        Task UpdateAsync(Product product, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Product>> SearchAsync(string searchTerm, CancellationToken ct = default);
        Task<IEnumerable<Product>> GetLowStockProductsAsync(CancellationToken ct = default);
        Task<IEnumerable<Product>> GetSlowSellingProductsAsync(int daysToConsider = 30, int minSalesCount = 5, CancellationToken ct = default);
    }
}
