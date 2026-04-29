using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class CustomerPaymentRepository : ICustomerPaymentRepository
    {
        private readonly AppDbContext _context;

        public CustomerPaymentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CustomerPayment>> GetByCustomerIdAsync(int customerId, CancellationToken ct = default)
        {
            return await _context.CustomerPayments
                .Where(cp => cp.CustomerId == customerId)
                .OrderByDescending(cp => cp.PaymentDate)
                .ToListAsync(ct);
        }

        public async Task<CustomerPayment> AddAsync(CustomerPayment payment, CancellationToken ct = default)
        {
            _context.CustomerPayments.Add(payment);

            // Update customer's current balance
            var customer = await _context.Customers.FindAsync([payment.CustomerId], ct);
            if (customer != null)
            {
                customer.CurrentBalance -= payment.AmountPaid;
                customer.ModifiedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync(ct);
            return payment;
        }
    }
}
