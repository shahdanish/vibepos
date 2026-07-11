using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IMedicalRepRepository
    {
        Task<MedicalRep?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<MedicalRep>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<IEnumerable<MedicalRep>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default);

        /// <summary>Case-insensitive existing-name check used to prevent duplicate reps.</summary>
        Task<MedicalRep?> FindByNameAsync(string name, CancellationToken ct = default);
        Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default);

        Task<MedicalRep> AddAsync(MedicalRep rep, CancellationToken ct = default);
        Task UpdateAsync(MedicalRep rep, CancellationToken ct = default);
        Task SoftDeleteAsync(int id, CancellationToken ct = default);
    }
}
