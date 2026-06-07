using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> ValidateUserAsync(string username, string password, CancellationToken ct = default);
        Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
        Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<User> CreateAsync(User user, CancellationToken ct = default);
        Task UpdateAsync(User user, CancellationToken ct = default);
        Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<User>> GetAllWithRolesAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<bool> IsUsernameUniqueAsync(string username, int? excludeId = null, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
