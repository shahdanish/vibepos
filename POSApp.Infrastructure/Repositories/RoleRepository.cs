using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _context;

        public RoleRepository(AppDbContext context) => _context = context;

        public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken ct = default)
            => await _context.Roles
                .Include(r => r.RolePermissions)
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

        public async Task<Role?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _context.Roles.FindAsync(new object[] { id }, ct);

        public async Task<Role?> GetWithPermissionsAsync(int id, CancellationToken ct = default)
            => await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task<Role> CreateAsync(Role role, CancellationToken ct = default)
        {
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(ct);
            return role;
        }

        public async Task UpdateAsync(Role role, CancellationToken ct = default)
        {
            _context.Entry(role).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var role = await _context.Roles.FindAsync(new object[] { id }, ct);
            if (role != null)
            {
                _context.Roles.Remove(role);
                await _context.SaveChangesAsync(ct);
            }
        }

        public async Task SetPermissionsAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken ct = default)
        {
            var existing = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync(ct);
            _context.RolePermissions.RemoveRange(existing);

            foreach (var permId in permissionIds)
                _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permId });

            await _context.SaveChangesAsync(ct);
        }
    }
}
