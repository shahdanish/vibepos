using POSApp.Core.Services;
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

        public async Task<ShiftActivity> GetActivityAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var sales = await _context.Sales.AsNoTracking()
                .Where(s => s.SaleDate >= from && s.SaleDate <= to)
                .Include(s => s.Payments)
                .ToListAsync(ct);
            var expenses = (await _context.Expenses.AsNoTracking()
                    .Where(e => e.Date >= from && e.Date <= to)
                    .Select(e => e.Amount)
                    .ToListAsync(ct))
                .Sum();
            return new ShiftActivity(sales, expenses);
        }

        public async Task CloseShiftAsync(int shiftId, decimal actualClosingBalance, CancellationToken ct = default)
        {
            var shift = await _context.Shifts.FindAsync([shiftId], ct);
            if (shift != null)
            {
                // Calculate expected: opening + today's sales - today's expenses. US sales count
                // their cash tenders only; sales without tenders keep the original ReceiveCash sum.
                var sales = await _context.Sales.AsNoTracking()
                    .Where(s => s.SaleDate >= shift.OpenedAt)
                    .Include(s => s.Payments)
                    .ToListAsync(ct);
                var todaySales = sales.Sum(s => DrawerCash.For(s, x => x.ReceiveCash));

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
