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
        
        // Advanced Reports
        Task<IEnumerable<SalesByCategoryDto>> GetSalesByCategoryAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
        Task<IEnumerable<TopProductDto>> GetTopSellingProductsAsync(int count, DateTime startDate, DateTime endDate, CancellationToken ct = default);
        Task<decimal> GetTotalProfitAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
        Task<IEnumerable<SalesByPaymentTypeDto>> GetSalesByPaymentTypeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    }
    
    // Report DTOs
    public sealed class SalesByCategoryDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; }
    }
    
    public sealed class TopProductDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalProfit { get; set; }
    }
    
    public sealed class SalesByPaymentTypeDto
    {
        public string PaymentType { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
