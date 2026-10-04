using POSApp.Core.Services;

namespace POSApp.Core.Interfaces
{
    /// <summary>Reads and saves the shop's language, edited phrases and loyalty rates.</summary>
    public interface IShopTextStore
    {
        Task<ShopTextSettings> GetAsync(CancellationToken ct = default);
        Task SaveAsync(ShopTextSettings settings, CancellationToken ct = default);
    }
}
