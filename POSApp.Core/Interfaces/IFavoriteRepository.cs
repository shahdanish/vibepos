using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IFavoriteRepository
    {
        Task<IEnumerable<UserFavorite>> GetByUserIdAsync(int userId, CancellationToken ct = default);
        Task<UserFavorite> AddAsync(UserFavorite favorite, CancellationToken ct = default);
        Task DeleteAsync(int userId, int productId, CancellationToken ct = default);
        Task<bool> IsFavoriteAsync(int userId, int productId, CancellationToken ct = default);
    }
}
