using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ISaleRepository
    {
        Task<Sale?> GetByIdAsync(int id);
        Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task<IEnumerable<Sale>> GetAllAsync();
        Task<IEnumerable<Sale>> GetByDateAsync(DateTime date);
        Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Sale> AddAsync(Sale sale);
        Task UpdateAsync(Sale sale);
        Task DeleteAsync(int id);
        Task<string> GetNextInvoiceNumberAsync();
        Task<IEnumerable<SaleItem>> GetRecentSalesItemsAsync(int days = 30);
    }
}
