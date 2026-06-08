using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class EmployeeRepository : IEmployeeRepository
    {
        private readonly AppDbContext _context;

        public EmployeeRepository(AppDbContext context) => _context = context;

        public async Task<Employee?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await _context.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

        public async Task<IEnumerable<Employee>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default) =>
            await _context.Employees
                .Where(e => !e.IsDeleted && (includeInactive || e.IsActive))
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

        public async Task<IEnumerable<Employee>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default)
        {
            var lower = searchTerm.ToLower();
            return await _context.Employees
                .Where(e => !e.IsDeleted && (includeInactive || e.IsActive)
                    && (e.Name.ToLower().Contains(lower)
                        || e.EmployeeCode.ToLower().Contains(lower)
                        || (e.Cnic != null && e.Cnic.Contains(lower))
                        || (e.CellNumber != null && e.CellNumber.Contains(lower))
                        || e.Designation.ToLower().Contains(lower)
                        || (e.Department != null && e.Department.ToLower().Contains(lower))))
                .OrderBy(e => e.Name)
                .ToListAsync(ct);
        }

        public async Task<bool> CnicExistsAsync(string cnic, int? excludeId = null, CancellationToken ct = default) =>
            await _context.Employees.AnyAsync(e =>
                !e.IsDeleted &&
                e.Cnic != null && e.Cnic == cnic &&
                (excludeId == null || e.Id != excludeId), ct);

        public async Task<string> GenerateEmployeeCodeAsync(CancellationToken ct = default)
        {
            var maxId = await _context.Employees.IgnoreQueryFilters()
                .MaxAsync(e => (int?)e.Id, ct) ?? 0;
            return $"EMP-{(maxId + 1):D4}";
        }

        public async Task<Employee> AddAsync(Employee employee, CancellationToken ct = default)
        {
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(ct);
            return employee;
        }

        public async Task UpdateAsync(Employee employee, CancellationToken ct = default)
        {
            employee.ModifiedDate = DateTime.Now;
            _context.Employees.Update(employee);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
        {
            var emp = await _context.Employees.FindAsync(new object[] { id }, ct);
            if (emp != null)
            {
                emp.IsDeleted = true;
                emp.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
