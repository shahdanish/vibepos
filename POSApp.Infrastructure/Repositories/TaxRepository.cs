using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class TaxRepository : ITaxRepository
    {
        /// <summary>ApplicationSettings key: "true" when sales tax is charged.</summary>
        public const string EnabledSettingKey = "Tax.Enabled";

        public const string GeneralCategoryName = "General merchandise";
        public const string OtcCategoryName = "Non-prescription drugs (OTC)";

        private readonly AppDbContext _context;

        public TaxRepository(AppDbContext context) => _context = context;

        public async Task<TaxSettings> GetSettingsAsync(CancellationToken ct = default)
        {
            var enabled = await _context.ApplicationSettings
                .Where(s => s.Key == EnabledSettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);
            var categories = await _context.TaxCategories.AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .ToListAsync(ct);
            return new TaxSettings(string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase), categories);
        }

        public async Task SaveSettingsAsync(bool enabled, IReadOnlyList<TaxCategory> categories, CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var existing = await _context.TaxCategories.ToListAsync(ct);
            var keep = categories.Where(c => c.Id > 0).Select(c => c.Id).ToHashSet();

            // Deleted categories: their products go back to the default category.
            var removed = existing.Where(c => !keep.Contains(c.Id)).ToList();
            if (removed.Count > 0)
            {
                var removedIds = removed.Select(c => c.Id).ToList();
                var products = await _context.Products.IgnoreQueryFilters()
                    .Where(p => p.TaxCategoryId != null && removedIds.Contains(p.TaxCategoryId.Value))
                    .ToListAsync(ct);
                foreach (var product in products) product.TaxCategoryId = null;
                _context.TaxCategories.RemoveRange(removed);
            }

            var hasDefault = categories.Any(c => c.IsDefault);
            for (var i = 0; i < categories.Count; i++)
            {
                var source = categories[i];
                var target = source.Id > 0 ? existing.FirstOrDefault(c => c.Id == source.Id) : null;
                if (target == null)
                {
                    target = new TaxCategory { CreatedDate = DateTime.Now };
                    _context.TaxCategories.Add(target);
                }
                target.Name = source.Name.Trim();
                target.RatePercent = source.RatePercent;
                target.IsDefault = hasDefault ? source.IsDefault : i == 0;
                target.SortOrder = i + 1;
            }

            await SetEnabledAsync(enabled, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public async Task EnsureUsDefaultsAsync(decimal ratePercent, CancellationToken ct = default)
        {
            if (!await _context.TaxCategories.AnyAsync(ct))
            {
                var now = DateTime.Now;
                _context.TaxCategories.AddRange(
                    new TaxCategory { Name = GeneralCategoryName, RatePercent = ratePercent, IsDefault = true, SortOrder = 1, CreatedDate = now },
                    new TaxCategory { Name = OtcCategoryName, RatePercent = ratePercent, SortOrder = 2, CreatedDate = now },
                    new TaxCategory { Name = "Food & grocery", RatePercent = ratePercent, SortOrder = 3, CreatedDate = now },
                    new TaxCategory { Name = "Prescription drugs", RatePercent = 0m, SortOrder = 4, CreatedDate = now },
                    new TaxCategory { Name = "Non-taxable", RatePercent = 0m, SortOrder = 5, CreatedDate = now });
            }

            if (ratePercent > 0)
                await SetEnabledAsync(true, ct);

            await _context.SaveChangesAsync(ct);
        }

        private async Task SetEnabledAsync(bool enabled, CancellationToken ct)
        {
            var row = await _context.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == EnabledSettingKey, ct);
            var value = enabled ? "true" : "false";
            if (row == null)
            {
                _context.ApplicationSettings.Add(new ApplicationSetting
                {
                    Key = EnabledSettingKey,
                    Value = value,
                    Description = "Charge US sales tax on sales",
                    CreatedDate = DateTime.Now
                });
            }
            else if (row.Value != value)
            {
                row.Value = value;
                row.ModifiedDate = DateTime.Now;
            }
        }
    }
}
