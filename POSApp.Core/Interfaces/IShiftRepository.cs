using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IShiftRepository
    {
        Task<Shift?> GetCurrentOpenShiftAsync(CancellationToken ct = default);
        Task<IEnumerable<Shift>> GetAllAsync(CancellationToken ct = default);
        Task<Shift> OpenShiftAsync(decimal openingBalance, CancellationToken ct = default);
        Task CloseShiftAsync(int shiftId, decimal actualClosingBalance, CancellationToken ct = default);
    }
}
