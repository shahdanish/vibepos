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

        // ── Shop-wide quick keys ─────────────────────────────────────────────────

        public async Task<IReadOnlyList<Product>> GetQuickKeyProductsAsync(CancellationToken ct = default)
        {
            // One position per product: the lowest sort order, then the earliest star.
            var order = (await _context.UserFavorites
                    .Select(f => new { f.ProductId, f.SortOrder, f.AddedDate })
                    .ToListAsync(ct))
                .GroupBy(f => f.ProductId)
                .Select(g => new { ProductId = g.Key, SortOrder = g.Min(f => f.SortOrder), Added = g.Min(f => f.AddedDate) })
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Added)
                .Select(f => f.ProductId)
                .ToList();

            if (order.Count == 0)
                return Array.Empty<Product>();

            // The Product query filter drops soft-deleted products.
            var products = await _context.Products
                .Where(p => order.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            return order.Where(products.ContainsKey).Select(id => products[id]).ToList();
        }

        public async Task<IReadOnlySet<int>> GetQuickKeyProductIdsAsync(CancellationToken ct = default)
        {
            return (await _context.UserFavorites.Select(f => f.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        }

        public async Task SetQuickKeyAsync(int productId, int userId, bool isQuickKey, CancellationToken ct = default)
        {
            var existing = await _context.UserFavorites.Where(f => f.ProductId == productId).ToListAsync(ct);

            if (isQuickKey)
            {
                if (existing.Count > 0) return;

                var name = await _context.Products.IgnoreQueryFilters()
                    .Where(p => p.Id == productId).Select(p => p.ProductName).FirstOrDefaultAsync(ct);
                var last = await _context.UserFavorites.Select(f => (int?)f.SortOrder).MaxAsync(ct) ?? 0;

                _context.UserFavorites.Add(new UserFavorite
                {
                    UserId = userId,
                    ProductId = productId,
                    ProductName = name ?? string.Empty,
                    SortOrder = last + 1
                });
            }
            else
            {
                if (existing.Count == 0) return;
                _context.UserFavorites.RemoveRange(existing);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task ReorderQuickKeysAsync(IReadOnlyList<int> productIdsInOrder, CancellationToken ct = default)
        {
            var position = productIdsInOrder
                .Select((id, index) => (id, index))
                .GroupBy(x => x.id)
                .ToDictionary(g => g.Key, g => g.First().index + 1);

            var rows = await _context.UserFavorites.ToListAsync(ct);
            foreach (var row in rows)
                row.SortOrder = position.TryGetValue(row.ProductId, out var p) ? p : productIdsInOrder.Count + 1 + row.SortOrder;

            await _context.SaveChangesAsync(ct);
        }
    }
}
