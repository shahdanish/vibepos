using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IDoctorRepository
    {
        Task<Doctor?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Doctor>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<IEnumerable<Doctor>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default);
        Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default);
        Task<Doctor> AddAsync(Doctor doctor, CancellationToken ct = default);
        Task UpdateAsync(Doctor doctor, CancellationToken ct = default);
        Task SoftDeleteAsync(int id, CancellationToken ct = default);
    }
}
