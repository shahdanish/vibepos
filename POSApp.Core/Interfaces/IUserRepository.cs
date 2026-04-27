using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> ValidateUserAsync(string username, string password);
        Task<User?> GetByUsernameAsync(string username);
        Task<User> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task<IEnumerable<User>> GetAllAsync();
    }
}
