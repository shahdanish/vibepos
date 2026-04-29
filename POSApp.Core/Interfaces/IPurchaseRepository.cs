using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IPurchaseRepository
    {
        Task<PurchaseOrder?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<PurchaseOrder?> GetByPurchaseNumberAsync(string purchaseNumber, CancellationToken ct = default);
        Task<IEnumerable<PurchaseOrder>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<PurchaseOrder>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
        Task<PurchaseOrder> AddAsync(PurchaseOrder purchase, CancellationToken ct = default);
        Task UpdateAsync(PurchaseOrder purchase, CancellationToken ct = default);
        Task<string> GetNextPurchaseNumberAsync(CancellationToken ct = default);
    }
}
