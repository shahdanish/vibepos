using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Customer?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default);
        Task<IEnumerable<Customer>> GetAllAsync(CancellationToken ct = default);
        Task<Customer> AddAsync(Customer customer, CancellationToken ct = default);
        Task UpdateAsync(Customer customer, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Customer>> SearchAsync(string searchTerm, CancellationToken ct = default);
    }
}
