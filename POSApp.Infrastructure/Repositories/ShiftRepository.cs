using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class ShiftRepository : IShiftRepository
    {
        private readonly AppDbContext _context;

        public ShiftRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Shift?> GetCurrentOpenShiftAsync(CancellationToken ct = default)
        {
            return await _context.Shifts
                .FirstOrDefaultAsync(s => s.ClosedAt == null, ct);
        }

        public async Task<IEnumerable<Shift>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Shifts
                .OrderByDescending(s => s.OpenedAt)
                .ToListAsync(ct);
        }

        public async Task<Shift> OpenShiftAsync(decimal openingBalance, CancellationToken ct = default)
        {
            var shift = new Shift
            {
                OpenedAt = DateTime.Now,
                OpeningBalance = openingBalance
            };
            _context.Shifts.Add(shift);
            await _context.SaveChangesAsync(ct);
            return shift;
        }

        public async Task CloseShiftAsync(int shiftId, decimal actualClosingBalance, CancellationToken ct = default)
        {
            var shift = await _context.Shifts.FindAsync([shiftId], ct);
            if (shift != null)
            {
                // Calculate expected: opening + today's sales - today's expenses
                var todaySales = await _context.Sales
                    .Where(s => s.SaleDate >= shift.OpenedAt)
                    .SumAsync(s => s.ReceiveCash, ct);

                var todayExpenses = await _context.Expenses
                    .Where(e => e.Date >= shift.OpenedAt)
                    .SumAsync(e => e.Amount, ct);

                shift.ClosedAt = DateTime.Now;
                shift.ExpectedClosingBalance = shift.OpeningBalance + todaySales - todayExpenses;
                shift.ActualClosingBalance = actualClosingBalance;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
