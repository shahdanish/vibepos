using POSApp.Core.Entities;
using POSApp.Core.Services;

namespace POSApp.Core.Interfaces
{
    /// <summary>US pharmacy front store: the PSE logbook, its limits, expiry and the pharmacy roles.</summary>
    public interface IFrontStoreRepository
    {
        /// <summary>The purchaser's logbook lines since <paramref name="since"/>, for the purchase limits.</summary>
        Task<IReadOnlyList<PseLogEntry>> GetPsePurchasesAsync(string idNumber, DateTime since, CancellationToken ct = default);

        /// <summary>The logbook for a period (newest first).</summary>
        Task<IReadOnlyList<PseLogEntry>> GetPseLogAsync(DateTime from, DateTime to, CancellationToken ct = default);

        /// <summary>Products that expire on or before <paramref name="until"/> (soonest first).</summary>
        Task<IReadOnlyList<Product>> GetExpiringAsync(DateTime until, CancellationToken ct = default);

        Task<FrontStoreSettings> GetSettingsAsync(CancellationToken ct = default);

        Task SaveSettingsAsync(FrontStoreSettings settings, CancellationToken ct = default);

        /// <summary>
        /// Adds the "Pse.LogView" permission and the Pharmacist / Pharmacy Technician roles when
        /// they are missing. Matched by name, so a shop's own roles are never overwritten.
        /// </summary>
        Task EnsureRolesAsync(CancellationToken ct = default);
    }

    /// <summary>Front-store rules a shop can tighten for its state.</summary>
    public sealed record FrontStoreSettings(decimal PseDailyLimitMg, decimal PseThirtyDayLimitMg, bool BlockExpired)
    {
        public static FrontStoreSettings Default { get; } = new(PseLimits.FederalDailyMg, PseLimits.FederalThirtyDayMg, true);
    }
}
