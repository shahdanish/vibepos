using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;

namespace POSApp.Infrastructure.Services
{
    /// <summary>Shop language, edited phrases and loyalty rates, in ApplicationSettings.</summary>
    public sealed class ShopTextStore : IShopTextStore
    {
        public const string LanguageKey = "Shop.Language";
        public const string OverridesKey = "Shop.PhraseOverrides";
        public const string PointsPerDollarKey = "Loyalty.PointsPerDollar";
        public const string PointsPerRewardKey = "Loyalty.PointsPerRewardDollar";

        private readonly AppDbContext _db;

        public ShopTextStore(AppDbContext db) => _db = db;

        public async Task<ShopTextSettings> GetAsync(CancellationToken ct = default)
        {
            var rows = await _db.ApplicationSettings.AsNoTracking()
                .Where(s => s.Key == LanguageKey || s.Key == OverridesKey || s.Key == PointsPerDollarKey || s.Key == PointsPerRewardKey)
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

            var language = string.Equals(rows.GetValueOrDefault(LanguageKey), ShopPhrases.Spanish, StringComparison.OrdinalIgnoreCase)
                ? ShopPhrases.Spanish
                : ShopPhrases.English;

            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            if (rows.TryGetValue(OverridesKey, out var json) && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed != null)
                    {
                        foreach (var pair in parsed)
                        {
                            if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                                overrides[pair.Key] = pair.Value.Trim();
                        }
                    }
                }
                catch (JsonException)
                {
                    // A damaged row falls back to the built-in language.
                }
            }

            var perDollar = ParseDecimal(rows.GetValueOrDefault(PointsPerDollarKey), 1m);
            var perReward = ParseInt(rows.GetValueOrDefault(PointsPerRewardKey), 100);
            return new ShopTextSettings(language, overrides, perDollar, perReward);
        }

        public async Task SaveAsync(ShopTextSettings settings, CancellationToken ct = default)
        {
            var language = string.Equals(settings.Language, ShopPhrases.Spanish, StringComparison.OrdinalIgnoreCase)
                ? ShopPhrases.Spanish
                : ShopPhrases.English;
            var clean = settings.Overrides
                .Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value))
                .ToDictionary(p => p.Key.Trim(), p => p.Value.Trim());

            await Upsert(LanguageKey, language, "Cashier language: en or es", ct);
            await Upsert(OverridesKey, JsonSerializer.Serialize(clean), "Phrases the shop edited", ct);
            await Upsert(PointsPerDollarKey, settings.PointsPerDollar.ToString(System.Globalization.CultureInfo.InvariantCulture), "Loyalty points earned per dollar", ct);
            await Upsert(PointsPerRewardKey, Math.Max(1, settings.PointsPerRewardDollar).ToString(), "Loyalty points that equal one dollar off", ct);
            await _db.SaveChangesAsync(ct);
        }

        private async Task Upsert(string key, string value, string description, CancellationToken ct)
        {
            var row = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (row == null)
            {
                _db.ApplicationSettings.Add(new ApplicationSetting
                {
                    Key = key,
                    Value = value,
                    Description = description,
                    CreatedDate = DateTime.Now
                });
            }
            else
            {
                row.Value = value;
                row.ModifiedDate = DateTime.Now;
            }
        }

        private static decimal ParseDecimal(string? text, decimal fallback) =>
            decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0
                ? n : fallback;

        private static int ParseInt(string? text, int fallback) =>
            int.TryParse(text, out var n) && n > 0 ? n : fallback;
    }
}
