using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ICustomerPaymentRepository
    {
        Task<IEnumerable<CustomerPayment>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default);
        Task<CustomerPayment> AddAsync(CustomerPayment payment, CancellationToken ct = default);
    }
}
