using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class SalarySlipRepository : ISalarySlipRepository
    {
        private readonly AppDbContext _context;

        public SalarySlipRepository(AppDbContext context) => _context = context;

        public async Task<SalarySlip?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await _context.SalarySlips
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

        public async Task<IEnumerable<SalarySlip>> GetByEmployeeAsync(int employeeId, CancellationToken ct = default) =>
            await _context.SalarySlips
                .Include(s => s.Employee)
                .Where(s => s.EmployeeId == employeeId && !s.IsDeleted)
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.Month)
                .ToListAsync(ct);

        public async Task<bool> SlipExistsAsync(int employeeId, int month, int year, int? excludeId = null, CancellationToken ct = default) =>
            await _context.SalarySlips.AnyAsync(s =>
                !s.IsDeleted &&
                s.EmployeeId == employeeId &&
                s.Month == month &&
                s.Year == year &&
                (excludeId == null || s.Id != excludeId), ct);

        public async Task<string> GenerateSlipNumberAsync(CancellationToken ct = default)
        {
            var count = await _context.SalarySlips.IgnoreQueryFilters().CountAsync(ct);
            return $"SAL-{DateTime.Now:yyyyMM}-{(count + 1):D4}";
        }

        public async Task<SalarySlip> AddAsync(SalarySlip slip, CancellationToken ct = default)
        {
            _context.SalarySlips.Add(slip);
            await _context.SaveChangesAsync(ct);
            return slip;
        }

        public async Task UpdateAsync(SalarySlip slip, CancellationToken ct = default)
        {
            _context.SalarySlips.Update(slip);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
        {
            var slip = await _context.SalarySlips.FindAsync(new object[] { id }, ct);
            if (slip != null)
            {
                slip.IsDeleted = true;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
