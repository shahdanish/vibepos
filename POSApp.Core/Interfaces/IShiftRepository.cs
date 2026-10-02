using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IShiftRepository
    {
        Task<Shift?> GetCurrentOpenShiftAsync(CancellationToken ct = default);
        Task<IEnumerable<Shift>> GetAllAsync(CancellationToken ct = default);
        Task<Shift> OpenShiftAsync(decimal openingBalance, CancellationToken ct = default);
        Task CloseShiftAsync(int shiftId, decimal actualClosingBalance, CancellationToken ct = default);

        /// <summary>Sales and returns (with tenders) from <paramref name="from"/> up to <paramref name="to"/>, and expenses paid in that time.</summary>
        Task<ShiftActivity> GetActivityAsync(DateTime from, DateTime to, CancellationToken ct = default);
    }

    /// <summary>What happened in a shift's time window, for the X/Z report.</summary>
    public sealed record ShiftActivity(IReadOnlyList<Sale> Sales, decimal Expenses);
}
