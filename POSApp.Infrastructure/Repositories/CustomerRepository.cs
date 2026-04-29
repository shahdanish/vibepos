using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly AppDbContext _context;

        public CustomerRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Customers.FindAsync([id], ct);
        }

        public async Task<Customer?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default)
        {
            return await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        }

        public async Task<IEnumerable<Customer>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Customers.ToListAsync(ct);
        }

        public async Task<Customer> AddAsync(Customer customer, CancellationToken ct = default)
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(ct);
            return customer;
        }

        public async Task UpdateAsync(Customer customer, CancellationToken ct = default)
        {
            customer.ModifiedDate = DateTime.Now;
            _context.Entry(customer).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var customer = await _context.Customers.FindAsync([id], ct);
            if (customer != null)
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task<IEnumerable<Customer>> SearchAsync(string searchTerm, CancellationToken ct = default)
        {
            return await _context.Customers
                .Where(c => c.CustomerId.Contains(searchTerm) ||
                           c.Name.Contains(searchTerm) ||
                           (c.Phone != null && c.Phone.Contains(searchTerm)))
                .ToListAsync(ct);
        }
    }
}
