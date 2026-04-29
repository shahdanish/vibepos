using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ISupplierRepository
    {
        Task<Supplier?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Supplier?> GetBySupplierIdAsync(string supplierId, CancellationToken ct = default);
        Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<Supplier>> GetActiveAsync(CancellationToken ct = default);
        Task<Supplier> AddAsync(Supplier supplier, CancellationToken ct = default);
        Task UpdateAsync(Supplier supplier, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Supplier>> SearchAsync(string searchTerm, CancellationToken ct = default);
    }
}
