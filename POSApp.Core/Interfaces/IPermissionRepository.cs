using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IPermissionRepository
    {
        Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct = default);
    }
}
