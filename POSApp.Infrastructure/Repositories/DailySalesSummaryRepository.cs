using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class DailySalesSummaryRepository : IDailySalesSummaryRepository
    {
        private readonly AppDbContext _context;

        public DailySalesSummaryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DailySalesSummary?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.DailySalesSummaries.FindAsync(new object[] { id }, ct);
        }

        public async Task<DailySalesSummary?> GetByDateAsync(DateTime date, CancellationToken ct = default)
        {
            return await _context.DailySalesSummaries
                .FirstOrDefaultAsync(d => d.Date.Date == date.Date, ct);
        }

        public async Task<IEnumerable<DailySalesSummary>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.DailySalesSummaries
                .OrderByDescending(d => d.Date)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<DailySalesSummary>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        {
            return await _context.DailySalesSummaries
                .Where(d => d.Date >= startDate && d.Date <= endDate)
                .OrderByDescending(d => d.Date)
                .ToListAsync(ct);
        }

        public async Task<DailySalesSummary> AddAsync(DailySalesSummary summary, CancellationToken ct = default)
        {
            _context.DailySalesSummaries.Add(summary);
            await _context.SaveChangesAsync(ct);
            return summary;
        }

        public async Task UpdateAsync(DailySalesSummary summary, CancellationToken ct = default)
        {
            _context.DailySalesSummaries.Update(summary);
            await _context.SaveChangesAsync(ct);
        }
    }
}
