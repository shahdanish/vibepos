using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface IEmployeeRepository
    {
        Task<Employee?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Employee>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<IEnumerable<Employee>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default);
        Task<bool> CnicExistsAsync(string cnic, int? excludeId = null, CancellationToken ct = default);
        Task<string> GenerateEmployeeCodeAsync(CancellationToken ct = default);
        Task<Employee> AddAsync(Employee employee, CancellationToken ct = default);
        Task UpdateAsync(Employee employee, CancellationToken ct = default);
        Task SoftDeleteAsync(int id, CancellationToken ct = default);
    }
}
