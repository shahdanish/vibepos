using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class PermissionRepository : IPermissionRepository
    {
        private readonly AppDbContext _context;

        public PermissionRepository(AppDbContext context) => _context = context;

        public async Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct = default)
            => await _context.Permissions
                .OrderBy(p => p.Category)
                .ThenBy(p => p.DisplayName)
                .ToListAsync(ct);
    }
}
