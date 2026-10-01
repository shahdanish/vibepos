using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;
using POSApp.Infrastructure.SampleData;

namespace POSApp.Infrastructure.Services
{
    public sealed class FirstRunSetupService : IFirstRunSetupService
    {
        public const string CompletedSettingKey = "Setup.Completed";

        /// <summary>ApplicationSettings key holding <see cref="InstallOrigin"/> ("New" / "Upgraded").</summary>
        public const string InstallOriginSettingKey = "Install.Origin";

        /// <summary>Ids of the demo users created by the seed data (admin, cashier and two client logins).</summary>
        private static readonly int[] SeededUserIds = { 1, 2, 3, 4 };
        private static readonly int[] SeededProductIds = { 1, 2 };
        private static readonly int[] SeededCategoryIds = { 1, 2 };
        private const int AdminRoleId = 1;

        private readonly AppDbContext _db;

        public FirstRunSetupService(AppDbContext db) => _db = db;

        public async Task<bool> IsCompleteAsync(CancellationToken ct = default)
        {
            var value = await _db.ApplicationSettings
                .Where(s => s.Key == CompletedSettingKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<InstallOriginResult> RecordInstallOriginAsync(bool databaseExistedAtStart, CancellationToken ct = default)
        {
            var row = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == InstallOriginSettingKey, ct);
            if (row != null)
            {
                var known = Enum.TryParse<InstallOrigin>(row.Value, ignoreCase: true, out var parsed) ? parsed : InstallOrigin.Upgraded;
                return new InstallOriginResult(known, FirstSeen: false);
            }

            var origin = databaseExistedAtStart ? InstallOrigin.Upgraded : InstallOrigin.New;
            _db.ApplicationSettings.Add(new ApplicationSetting
            {
                Key = InstallOriginSettingKey,
                Value = origin.ToString(),
                Description = origin == InstallOrigin.New
                    ? "Database created with the US defaults"
                    : "Database in use before the US defaults; keeps its original behaviour",
                CreatedDate = DateTime.Now
            });
            await _db.SaveChangesAsync(ct);
            return new InstallOriginResult(origin, FirstSeen: true);
        }

        public async Task<FirstRunSetupResult> CompleteAsync(FirstRunSetupRequest request, CancellationToken ct = default)
        {
            var username = request.AdminUsername?.Trim() ?? string.Empty;
            if (username.Length < 3) throw new ArgumentException("Username must be at least 3 characters.");
            if (string.IsNullOrEmpty(request.AdminPassword) || request.AdminPassword.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // 1. Retire the demo logins. Delete them outright; if something already references a
            //    user (it shouldn't on a fresh install), deactivate it and scramble its password.
            var seeded = await _db.Users.Where(u => SeededUserIds.Contains(u.Id)).ToListAsync(ct);
            foreach (var user in seeded)
            {
                user.IsActive = false;
                user.PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N"));
            }
            await _db.SaveChangesAsync(ct);

            foreach (var user in seeded)
            {
                try
                {
                    _db.Users.Remove(user);
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    _db.Entry(user).State = EntityState.Unchanged; // keep it, already disabled
                }
            }

            // 2. The owner's account. Reuse the username if a (now disabled) seeded user holds it.
            var owner = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (owner != null)
            {
                owner.PasswordHash = PasswordHasher.Hash(request.AdminPassword);
                owner.RoleId = AdminRoleId;
                owner.IsActive = true;
            }
            else
            {
                owner = new User
                {
                    Username = username,
                    PasswordHash = PasswordHasher.Hash(request.AdminPassword),
                    RoleId = AdminRoleId,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };
                _db.Users.Add(owner);
            }
            await _db.SaveChangesAsync(ct);

            // 3. The two products and categories in the seed data come from the original
            //    Pakistani build ("Glycerin 25gm", "Glue Stick"); a new shop starts without them.
            try
            {
                var products = await _db.Products.IgnoreQueryFilters()
                    .Where(p => SeededProductIds.Contains(p.Id)).ToListAsync(ct);
                _db.Products.RemoveRange(products);
                await _db.SaveChangesAsync(ct);

                var emptyCategories = await _db.Categories
                    .Where(c => SeededCategoryIds.Contains(c.Id) &&
                                !_db.Products.IgnoreQueryFilters().Any(p => p.CategoryId == c.Id))
                    .ToListAsync(ct);
                _db.Categories.RemoveRange(emptyCategories);
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Already used somewhere — leaving them is harmless.
                _db.ChangeTracker.Clear();
            }

            // 4. Optional sample data: US over-the-counter catalogue, customers, quick keys, logins.
            var result = request.LoadSampleData
                ? await UsPharmacySampleData.LoadAsync(_db, owner.Id, DateTime.Today, ct)
                : FirstRunSetupResult.NoSamples;

            // 5. Remember that setup is done.
            var flag = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == CompletedSettingKey, ct);
            if (flag == null)
            {
                _db.ApplicationSettings.Add(new ApplicationSetting
                {
                    Key = CompletedSettingKey,
                    Value = "true",
                    Description = "First-run setup (owner account) has been completed",
                    CreatedDate = DateTime.Now
                });
            }
            else
            {
                flag.Value = "true";
                flag.ModifiedDate = DateTime.Now;
            }
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            return result;
        }
    }
}
