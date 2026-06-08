using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ISalarySlipRepository
    {
        Task<SalarySlip?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<SalarySlip>> GetByEmployeeAsync(int employeeId, CancellationToken ct = default);
        Task<bool> SlipExistsAsync(int employeeId, int month, int year, int? excludeId = null, CancellationToken ct = default);
        Task<string> GenerateSlipNumberAsync(CancellationToken ct = default);
        Task<SalarySlip> AddAsync(SalarySlip slip, CancellationToken ct = default);
        Task UpdateAsync(SalarySlip slip, CancellationToken ct = default);
        Task SoftDeleteAsync(int id, CancellationToken ct = default);
    }
}
