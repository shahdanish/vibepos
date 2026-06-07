using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class PharmacyRepository : IPharmacyRepository
    {
        private readonly AppDbContext _context;

        public PharmacyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Pharmacy?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Pharmacies
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        }

        public async Task<IEnumerable<Pharmacy>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            return await _context.Pharmacies
                .Where(p => !p.IsDeleted && (includeInactive || p.IsActive))
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Pharmacy>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default)
        {
            var lower = searchTerm.ToLower();
            return await _context.Pharmacies
                .Where(p => !p.IsDeleted && (includeInactive || p.IsActive)
                    && (p.Name.ToLower().Contains(lower)
                        || (p.Area != null && p.Area.ToLower().Contains(lower))
                        || (p.City != null && p.City.ToLower().Contains(lower))
                        || (p.LicenseNo != null && p.LicenseNo.ToLower().Contains(lower))))
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
        }

        public async Task<bool> IsLicenseNoUniqueAsync(string licenseNo, int? excludeId = null, CancellationToken ct = default)
        {
            return !await _context.Pharmacies
                .AnyAsync(p => !p.IsDeleted
                    && p.LicenseNo == licenseNo
                    && (excludeId == null || p.Id != excludeId), ct);
        }

        public async Task<Pharmacy> AddAsync(Pharmacy pharmacy, CancellationToken ct = default)
        {
            _context.Pharmacies.Add(pharmacy);
            await _context.SaveChangesAsync(ct);
            return pharmacy;
        }

        public async Task UpdateAsync(Pharmacy pharmacy, CancellationToken ct = default)
        {
            pharmacy.ModifiedDate = DateTime.Now;
            _context.Pharmacies.Update(pharmacy);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
        {
            var pharmacy = await _context.Pharmacies.FindAsync(new object[] { id }, ct);
            if (pharmacy != null)
            {
                pharmacy.IsDeleted = true;
                pharmacy.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
