using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IFavoriteRepository
    {
        Task<IEnumerable<UserFavorite>> GetByUserIdAsync(int userId, CancellationToken ct = default);
        Task<UserFavorite> AddAsync(UserFavorite favorite, CancellationToken ct = default);
        Task DeleteAsync(int userId, int productId, CancellationToken ct = default);
        Task<bool> IsFavoriteAsync(int userId, int productId, CancellationToken ct = default);

        // ── Shop-wide quick keys (the sale screen's one-click tiles) ─────────────

        /// <summary>
        /// Products starred by anyone, in tile order. Deleted products are left out; each
        /// product appears once even when several users starred it.
        /// </summary>
        Task<IReadOnlyList<Product>> GetQuickKeyProductsAsync(CancellationToken ct = default);

        /// <summary>Key ids (<see cref="Product.Id"/>) of every product that is a quick key.</summary>
        Task<IReadOnlySet<int>> GetQuickKeyProductIdsAsync(CancellationToken ct = default);

        /// <summary>
        /// Turns a product's quick key on (recorded against <paramref name="userId"/>, placed
        /// last) or off (removed for everyone). Doing what is already the case is a no-op.
        /// </summary>
        Task SetQuickKeyAsync(int productId, int userId, bool isQuickKey, CancellationToken ct = default);

        /// <summary>Saves a new tile order; products not listed keep their position after the listed ones.</summary>
        Task ReorderQuickKeysAsync(IReadOnlyList<int> productIdsInOrder, CancellationToken ct = default);
    }
}
