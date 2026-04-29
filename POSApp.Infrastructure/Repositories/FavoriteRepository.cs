using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class FavoriteRepository : IFavoriteRepository
    {
        private readonly AppDbContext _context;

        public FavoriteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserFavorite>> GetByUserIdAsync(int userId, CancellationToken ct = default)
        {
            return await _context.UserFavorites
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.AddedDate)
                .ToListAsync(ct);
        }

        public async Task<UserFavorite> AddAsync(UserFavorite favorite, CancellationToken ct = default)
        {
            _context.UserFavorites.Add(favorite);
            await _context.SaveChangesAsync(ct);
            return favorite;
        }

        public async Task DeleteAsync(int userId, int productId, CancellationToken ct = default)
        {
            var favorite = await _context.UserFavorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);
            
            if (favorite != null)
            {
                _context.UserFavorites.Remove(favorite);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<bool> IsFavoriteAsync(int userId, int productId, CancellationToken ct = default)
        {
            return await _context.UserFavorites
                .AnyAsync(f => f.UserId == userId && f.ProductId == productId, ct);
        }
    }
}
