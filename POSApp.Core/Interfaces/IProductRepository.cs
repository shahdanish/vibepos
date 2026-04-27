using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByProductIdAsync(string productId);
        Task<IEnumerable<Product>> GetAllAsync();
        Task<IEnumerable<Product>> GetAllIncludingDeletedAsync();
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<Product> AddAsync(Product product);
        Task UpdateAsync(Product product);
        Task DeleteAsync(int id);
        Task<IEnumerable<Product>> SearchAsync(string searchTerm);
        Task<IEnumerable<Product>> GetLowStockProductsAsync();
        Task<IEnumerable<Product>> GetSlowSellingProductsAsync(int daysToConsider = 30, int minSalesCount = 5);
    }
}
