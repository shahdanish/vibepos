using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IGiftCardRepository
    {
        Task<GiftCard?> GetByCodeAsync(string code, CancellationToken ct = default);
        Task AddAsync(GiftCard card, CancellationToken ct = default);
        Task UpdateAsync(GiftCard card, CancellationToken ct = default);
    }
}
