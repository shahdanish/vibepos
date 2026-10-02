using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    /// <summary>Sales-tax configuration. It lives in the database so backups carry it.</summary>
    public interface ITaxRepository
    {
        Task<TaxSettings> GetSettingsAsync(CancellationToken ct = default);

        /// <summary>
        /// Saves the on/off switch and the categories: existing ones (Id &gt; 0) are updated, new
        /// ones added, and any not in the list removed (their products fall back to the default).
        /// </summary>
        Task SaveSettingsAsync(bool enabled, IReadOnlyList<TaxCategory> categories, CancellationToken ct = default);

        /// <summary>
        /// Creates the usual US front-store categories when there are none yet, taxing the taxable
        /// ones at <paramref name="ratePercent"/>, and switches tax on when the rate is above 0.
        /// </summary>
        Task EnsureUsDefaultsAsync(decimal ratePercent, CancellationToken ct = default);
    }

    /// <summary>The shop's sales-tax setup.</summary>
    public sealed record TaxSettings(bool Enabled, IReadOnlyList<TaxCategory> Categories)
    {
        public static TaxSettings Off { get; } = new(false, Array.Empty<TaxCategory>());

        public TaxCategory? Default => Categories.FirstOrDefault(c => c.IsDefault) ?? Categories.FirstOrDefault();

        /// <summary>The rate for a product's tax category (its own, else the default). 0 when tax is off.</summary>
        public decimal RateFor(int? taxCategoryId)
        {
            if (!Enabled) return 0m;
            var category = (taxCategoryId is int id ? Categories.FirstOrDefault(c => c.Id == id) : null) ?? Default;
            return category?.RatePercent ?? 0m;
        }
    }
}
