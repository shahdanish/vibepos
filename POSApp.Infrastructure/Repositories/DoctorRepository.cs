using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class DoctorRepository : IDoctorRepository
    {
        private readonly AppDbContext _context;

        public DoctorRepository(AppDbContext context) => _context = context;

        public async Task<Doctor?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await _context.Doctors.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

        public async Task<IEnumerable<Doctor>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default) =>
            await _context.Doctors
                .Where(d => !d.IsDeleted && (includeInactive || d.IsActive))
                .OrderBy(d => d.Name)
                .ToListAsync(ct);

        public async Task<IEnumerable<Doctor>> SearchAsync(string searchTerm, bool includeInactive = false, CancellationToken ct = default)
        {
            var lower = searchTerm.ToLower();
            return await _context.Doctors
                .Where(d => !d.IsDeleted && (includeInactive || d.IsActive)
                    && (d.Name.ToLower().Contains(lower)
                        || (d.Phone != null && d.Phone.Contains(lower))
                        || (d.City != null && d.City.ToLower().Contains(lower))))
                .OrderBy(d => d.Name)
                .ToListAsync(ct);
        }

        public async Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken ct = default) =>
            await _context.Doctors.AnyAsync(d =>
                !d.IsDeleted &&
                d.Name.ToLower() == name.ToLower() &&
                (excludeId == null || d.Id != excludeId), ct);

        public async Task<Doctor> AddAsync(Doctor doctor, CancellationToken ct = default)
        {
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync(ct);
            return doctor;
        }

        public async Task UpdateAsync(Doctor doctor, CancellationToken ct = default)
        {
            doctor.ModifiedDate = DateTime.Now;
            _context.Doctors.Update(doctor);
            await _context.SaveChangesAsync(ct);
        }

        public async Task SoftDeleteAsync(int id, CancellationToken ct = default)
        {
            var doctor = await _context.Doctors.FindAsync(new object[] { id }, ct);
            if (doctor != null)
            {
                doctor.IsDeleted = true;
                doctor.ModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }
        }
    }
}
