using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ICategoryRepository
    {
        Task<Category?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Category>> GetAllAsync(CancellationToken ct = default);
        Task<Category> AddAsync(Category category, CancellationToken ct = default);
        Task UpdateAsync(Category category, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Category>> SearchAsync(string searchTerm, CancellationToken ct = default);
    }
}
