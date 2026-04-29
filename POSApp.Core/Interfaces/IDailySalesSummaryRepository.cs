using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IDailySalesSummaryRepository
    {
        Task<DailySalesSummary?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<DailySalesSummary?> GetByDateAsync(DateTime date, CancellationToken ct = default);
        Task<IEnumerable<DailySalesSummary>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<DailySalesSummary>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
        Task<DailySalesSummary> AddAsync(DailySalesSummary summary, CancellationToken ct = default);
        Task UpdateAsync(DailySalesSummary summary, CancellationToken ct = default);
    }
}
