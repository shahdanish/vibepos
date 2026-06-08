using POSApp.Core.Entities;

namespace POSApp.Core.Interfaces
{
    public interface ICustomerPaymentRepository
    {
        Task<IEnumerable<CustomerPayment>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default);
        Task<CustomerPayment> AddAsync(CustomerPayment payment, CancellationToken ct = default);

        /// <summary>Updates an existing payment, adjusting the customer's balance by the difference.</summary>
        Task UpdateAsync(CustomerPayment payment, CancellationToken ct = default);

        /// <summary>Deletes a payment and refunds its amount back to the customer's balance.</summary>
        Task DeleteAsync(int paymentId, CancellationToken ct = default);
    }
}
