using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ISaleRepository
    {
        Task<Sale?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default);
        Task<IEnumerable<Sale>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<Sale>> GetByDateAsync(DateTime date, CancellationToken ct = default);
        Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
        Task<Sale> AddAsync(Sale sale, CancellationToken ct = default);
        Task UpdateAsync(Sale sale, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<string> GetNextInvoiceNumberAsync(CancellationToken ct = default);
        Task<IEnumerable<SaleItem>> GetRecentSalesItemsAsync(int days = 30, CancellationToken ct = default);
    }
}
