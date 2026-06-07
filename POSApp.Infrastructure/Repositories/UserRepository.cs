using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> ValidateUserAsync(string username, string password, CancellationToken ct = default)
        {
            return await _context.Users
                .Include(u => u.UserRole)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == username &&
                                         u.PasswordHash == password &&
                                         u.IsActive, ct);
        }

        public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        {
            return await _context.Users
                .Include(u => u.UserRole)
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);
        }

        public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Users
                .Include(u => u.UserRole)
                .FirstOrDefaultAsync(u => u.Id == id, ct);
        }

        public async Task<User> CreateAsync(User user, CancellationToken ct = default)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);
            return user;
        }

        public async Task UpdateAsync(User user, CancellationToken ct = default)
        {
            user.ModifiedDate = DateTime.Now;
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
        {
            return await _context.Users
                .Include(u => u.UserRole)
                .Where(u => u.IsActive)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<User>> GetAllWithRolesAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            var query = _context.Users.Include(u => u.UserRole).AsQueryable();
            if (!includeInactive)
                query = query.Where(u => u.IsActive);
            return await query.OrderBy(u => u.Username).ToListAsync(ct);
        }

        public async Task<bool> IsUsernameUniqueAsync(string username, int? excludeId = null, CancellationToken ct = default)
        {
            return !await _context.Users.AnyAsync(u =>
                u.Username == username && (excludeId == null || u.Id != excludeId), ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var user = await _context.Users.FindAsync(new object[] { id }, ct);
            if (user != null)
            {
                user.IsActive = false;
                user.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
