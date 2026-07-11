using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class MedicalRepRepository : IMedicalRepRepository
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserContext _user;

        public MedicalRepRepository(AppDbContext context, ICurrentUserContext user)
        {
            _context = context;
            _user = user;
        }

        // Master data for the call-scheduling module — gated to the same permission as the module.
        private void EnsureAuthorized()
        {
            if (!_user.HasPermission(Permissions.CallScheduleManage))
                throw new UnauthorizedAccessException("You do not have permission to manage medical representatives.");
        }

        public async Task<MedicalRep?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await _context.MedicalReps.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

        public async Task<IEnumerable<MedicalRep>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default) =>
            await _context.MedicalReps
                .Where(r => !r.IsDeleted && (includeInactive || r.IsActive))
                .OrderBy(r => r.Name)
                .ToListAsync(ct);

        public async Task<IEnumerable<MedicalRep>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default)
        {
            var lower = searchTerm.ToLower();
            return await _context.MedicalReps
                .Where(r => !r.IsDeleted && (includeInactive || r.IsActive)
                    && (r.Name.ToLower().Contains(lower)
                        || (r.Company != null && r.Company.ToLower().Contains(lower))
                        || (r.Phone != null && r.Phone.Contains(lower))))
                .OrderBy(r => r.Name)
                .ToListAsync(ct);
        }

        public async Task<MedicalRep?> FindByNameAsync(string name, CancellationToken ct = default)
        {
            var lower = name.Trim().ToLower();
            return await _context.MedicalReps.FirstOrDefaultAsync(
                r => !r.IsDeleted && r.Name.ToLower() == lower, ct);
        }

        public async Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default) =>
            await _context.MedicalReps.AnyAsync(r =>
                !r.IsDeleted &&
                r.Name.ToLower() == name.ToLower() &&
                (excludeId == null || r.Id != excludeId), ct);

        public async Task<MedicalRep> AddAsync(MedicalRep rep, CancellationToken ct = default)
        {
            EnsureAuthorized();
            _context.MedicalReps.Add(rep);
            await _context.SaveChangesAsync(ct);
            return rep;
        }

        public async Task UpdateAsync(MedicalRep rep, CancellationToken ct = default)
        {
            EnsureAuthorized();
            rep.ModifiedDate = DateTime.Now;
            _context.MedicalReps.Update(rep);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
        {
            EnsureAuthorized();
            var rep = await _context.MedicalReps.FindAsync(new object[] { id }, ct);
            if (rep != null)
            {
                rep.IsDeleted = true;
                rep.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
