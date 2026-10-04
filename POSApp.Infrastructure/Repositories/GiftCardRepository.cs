using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class GiftCardRepository : IGiftCardRepository
    {
        private readonly AppDbContext _db;

        public GiftCardRepository(AppDbContext db) => _db = db;

        public Task<GiftCard?> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            var key = GiftCardRules.Normalize(code);
            return _db.GiftCards.FirstOrDefaultAsync(c => c.Code == key, ct);
        }

        public async Task AddAsync(GiftCard card, CancellationToken ct = default)
        {
            card.Code = GiftCardRules.Normalize(card.Code);
            _db.GiftCards.Add(card);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(GiftCard card, CancellationToken ct = default)
        {
            _db.GiftCards.Update(card);
            await _db.SaveChangesAsync(ct);
        }
    }
}
