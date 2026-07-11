using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class CallScheduleRepository : ICallScheduleRepository
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserContext _user;

        public CallScheduleRepository(AppDbContext context, ICurrentUserContext user)
        {
            _context = context;
            _user = user;
        }

        // Enforce the module permission below the UI (defense-in-depth): both reading the
        // schedule and mutating it require CallSchedule.Manage, which is seeded only to
        // the PharmacyUser role.
        private void EnsureAuthorized()
        {
            if (!_user.HasPermission(Permissions.CallScheduleManage))
                throw new UnauthorizedAccessException("You do not have permission to access call schedules.");
        }

        public async Task<IReadOnlyList<CallSchedule>> GetByDateAsync(DateOnly date, CancellationToken ct = default)
        {
            EnsureAuthorized();
            // AsNoTracking: the app uses one long-lived DbContext, so always read fresh
            // DB state rather than a cached (possibly stale) tracked entity.
            return await _context.CallSchedules
                .AsNoTracking()
                .Include(c => c.Doctor)
                .Include(c => c.MedicalRep)
                .Where(c => c.ScheduleDate == date)
                .OrderBy(c => c.Id)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CallSchedule>> GetByRangeAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
        {
            EnsureAuthorized();
            return await _context.CallSchedules
                .AsNoTracking()
                .Where(c => c.ScheduleDate >= start && c.ScheduleDate <= end)
                .ToListAsync(ct);
        }

        public async Task<CallSchedule> AddAsync(CallSchedule schedule, CancellationToken ct = default)
        {
            EnsureAuthorized();
            _context.CallSchedules.Add(schedule);
            await _context.SaveChangesAsync(ct);
            return schedule;
        }

        public async Task<bool> MarkCallDoneAsync(int scheduleId, int doneByUserId, CancellationToken ct = default)
        {
            EnsureAuthorized();

            // Load the row and guard double-submit: if it's already done (or missing), do
            // nothing and report false. Otherwise stamp it done and persist via SaveChanges.
            var entity = await _context.CallSchedules.FirstOrDefaultAsync(c => c.Id == scheduleId, ct);
            if (entity == null || entity.IsCallDone)
                return false;

            entity.IsCallDone = true;
            entity.CallDoneAt = DateTime.Now;
            entity.CallDoneByUserId = doneByUserId;
            await _context.SaveChangesAsync(ct);
            return true;
        }
    }
}
