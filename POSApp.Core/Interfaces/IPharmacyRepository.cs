using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IPharmacyRepository
    {
        Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Pharmacy>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<IEnumerable<Pharmacy>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default);
        Task<bool> IsLicenseNoUniqueAsync(string licenseNo, int? excludeId = null, CancellationToken ct = default);
        Task<Pharmacy> AddAsync(Pharmacy pharmacy, CancellationToken ct = default);
        Task UpdateAsync(Pharmacy pharmacy, CancellationToken ct = default);
        Task SoftDeleteAsync(int id, CancellationToken ct = default);
    }
}
