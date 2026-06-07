using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IRoleRepository
    {
        Task<IEnumerable<Role>> GetAllAsync(CancellationToken ct = default);
        Task<Role?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Role?> GetWithPermissionsAsync(int id, CancellationToken ct = default);
        Task<Role> CreateAsync(Role role, CancellationToken ct = default);
        Task UpdateAsync(Role role, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task SetPermissionsAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken ct = default);
    }
}
