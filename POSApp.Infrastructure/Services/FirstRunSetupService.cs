using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;

namespace POSApp.Infrastructure.Services
{
    public sealed class FirstRunSetupService : IFirstRunSetupService
    {
        public const string CompletedSettingKey = "Setup.Completed";

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

        public async Task CompleteAsync(FirstRunSetupRequest request, CancellationToken ct = default)
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
            var clash = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (clash != null)
            {
                clash.PasswordHash = PasswordHasher.Hash(request.AdminPassword);
                clash.RoleId = AdminRoleId;
                clash.IsActive = true;
            }
            else
            {
                _db.Users.Add(new User
                {
                    Username = username,
                    PasswordHash = PasswordHasher.Hash(request.AdminPassword),
                    RoleId = AdminRoleId,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
            }
            await _db.SaveChangesAsync(ct);

            // 3. Demo catalogue (two sample products and their categories).
            if (!request.KeepSampleProducts)
            {
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
                    // Already used somewhere — leaving the samples is harmless.
                    _db.ChangeTracker.Clear();
                }
            }

            // 4. Remember that setup is done.
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
        }
    }
}
