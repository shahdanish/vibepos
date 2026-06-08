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
            // Validate payment
            if (payment.AmountPaid <= 0)
                throw new ArgumentException("Payment amount must be greater than zero.", nameof(payment.AmountPaid));

            // Verify customer exists
            var customer = await _context.Customers.FindAsync([payment.CustomerId], ct);
            if (customer == null)
                throw new InvalidOperationException($"Customer with ID {payment.CustomerId} not found.");

            // Add payment record
            _context.CustomerPayments.Add(payment);

            // Update customer's current balance (subtract payment from balance)
            customer.CurrentBalance -= payment.AmountPaid;
            customer.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync(ct);
            return payment;
        }

        public async Task UpdateAsync(CustomerPayment payment, CancellationToken ct = default)
        {
            if (payment.AmountPaid <= 0)
                throw new ArgumentException("Payment amount must be greater than zero.", nameof(payment.AmountPaid));

            var existing = await _context.CustomerPayments.FindAsync([payment.Id], ct);
            if (existing == null)
                throw new InvalidOperationException($"Payment with ID {payment.Id} not found.");

            var customer = await _context.Customers.FindAsync([existing.CustomerId], ct);

            // Reverse the old amount, then apply the new amount, to the customer balance.
            if (customer != null)
            {
                customer.CurrentBalance += existing.AmountPaid;   // refund old
                customer.CurrentBalance -= payment.AmountPaid;    // re-apply new
                customer.ModifiedDate = DateTime.Now;
            }

            existing.AmountPaid    = payment.AmountPaid;
            existing.Note          = payment.Note;
            existing.InvoiceNumber = payment.InvoiceNumber;
            existing.PaymentDate   = payment.PaymentDate;

            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int paymentId, CancellationToken ct = default)
        {
            var existing = await _context.CustomerPayments.FindAsync([paymentId], ct);
            if (existing == null) return;

            // Refund the paid amount back onto the customer's outstanding balance.
            var customer = await _context.Customers.FindAsync([existing.CustomerId], ct);
            if (customer != null)
            {
                customer.CurrentBalance += existing.AmountPaid;
                customer.ModifiedDate = DateTime.Now;
            }

            _context.CustomerPayments.Remove(existing);
            await _context.SaveChangesAsync(ct);
        }
    }
}
