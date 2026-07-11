using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ICallScheduleRepository
    {
        /// <summary>All (non-deleted) scheduled calls for a single date, with Doctor + Rep loaded.</summary>
        Task<IReadOnlyList<CallSchedule>> GetByDateAsync(DateOnly date, CancellationToken ct = default);

        /// <summary>
        /// All scheduled calls in the inclusive date range, used to build calendar day
        /// indicators (none / has-scheduled / all-done) for the visible month or week.
        /// </summary>
        Task<IReadOnlyList<CallSchedule>> GetByRangeAsync(DateOnly start, DateOnly end, CancellationToken ct = default);

        Task<CallSchedule> AddAsync(CallSchedule schedule, CancellationToken ct = default);

        /// <summary>
        /// Atomically marks a call done (guards double-submit / concurrent marking): only
        /// the first caller to flip a still-pending row succeeds. Returns false if the row
        /// was already done or does not exist.
        /// </summary>
        Task<bool> MarkCallDoneAsync(int scheduleId, int doneByUserId, CancellationToken ct = default);
    }
}
