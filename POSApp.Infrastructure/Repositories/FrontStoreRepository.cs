using System.Globalization;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Repositories
{
    public sealed class FrontStoreRepository : IFrontStoreRepository
    {
        public const string DailyLimitKey = "Pse.DailyLimitMg";
        public const string ThirtyDayLimitKey = "Pse.ThirtyDayLimitMg";
        public const string BlockExpiredKey = "Sale.BlockExpired";

        public const string PharmacistRole = "Pharmacist";
        public const string TechnicianRole = "Pharmacy Technician";

        private readonly AppDbContext _db;

        public FrontStoreRepository(AppDbContext db) => _db = db;

        public async Task<IReadOnlyList<PseLogEntry>> GetPsePurchasesAsync(string idNumber, DateTime since, CancellationToken ct = default)
        {
            var id = PseLogEntry.NormalizeId(idNumber);
            return await _db.PseLogEntries.AsNoTracking()
                .Where(e => e.IdNumber == id && e.PurchaseDate >= since)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PseLogEntry>> GetPseLogAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var end = to.Date.AddDays(1);
            return await _db.PseLogEntries.AsNoTracking()
                .Where(e => e.PurchaseDate >= from.Date && e.PurchaseDate < end)
                .OrderByDescending(e => e.PurchaseDate)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Product>> GetExpiringAsync(DateTime until, CancellationToken ct = default)
        {
            var products = await _db.Products.AsNoTracking()
                .Where(p => p.ExpiryDate != null && p.ExpiryDate <= until)
                .ToListAsync(ct);
            return products.OrderBy(p => p.ExpiryDate).ThenBy(p => p.ProductName).ToList();
        }

        public async Task<FrontStoreSettings> GetSettingsAsync(CancellationToken ct = default)
        {
            var rows = await _db.ApplicationSettings.AsNoTracking()
                .Where(s => s.Key == DailyLimitKey || s.Key == ThirtyDayLimitKey || s.Key == BlockExpiredKey)
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
            var d = FrontStoreSettings.Default;

            decimal Mg(string key, decimal fallback) =>
                rows.TryGetValue(key, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var mg) && mg > 0 ? mg : fallback;

            return new FrontStoreSettings(
                Mg(DailyLimitKey, d.PseDailyLimitMg),
                Mg(ThirtyDayLimitKey, d.PseThirtyDayLimitMg),
                rows.TryGetValue(BlockExpiredKey, out var block) ? !string.Equals(block, "false", StringComparison.OrdinalIgnoreCase) : d.BlockExpired);
        }

        public async Task SaveSettingsAsync(FrontStoreSettings settings, CancellationToken ct = default)
        {
            await Set(DailyLimitKey, settings.PseDailyLimitMg.ToString(CultureInfo.InvariantCulture), "PSE limit per purchaser per day (mg base)", ct);
            await Set(ThirtyDayLimitKey, settings.PseThirtyDayLimitMg.ToString(CultureInfo.InvariantCulture), "PSE limit per purchaser per 30 days (mg base)", ct);
            await Set(BlockExpiredKey, settings.BlockExpired ? "true" : "false", "Refuse to sell products past their expiry date", ct);
            await _db.SaveChangesAsync(ct);
        }

        private async Task Set(string key, string value, string description, CancellationToken ct)
        {
            var row = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (row == null)
                _db.ApplicationSettings.Add(new ApplicationSetting { Key = key, Value = value, Description = description, CreatedDate = DateTime.Now });
            else if (row.Value != value)
            {
                row.Value = value;
                row.ModifiedDate = DateTime.Now;
            }
        }

        public async Task EnsureRolesAsync(CancellationToken ct = default)
        {
            var pseLog = await _db.Permissions.FirstOrDefaultAsync(p => p.Name == Permissions.PseLogView, ct);
            var newPermission = pseLog == null;
            if (newPermission)
            {
                pseLog = new Permission { Name = Permissions.PseLogView, DisplayName = "View PSE Logbook", Category = "Front Store" };
                _db.Permissions.Add(pseLog);
                await _db.SaveChangesAsync(ct);

                // The full-access role gets it straight away.
                var admin = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct);
                if (admin != null)
                    _db.RolePermissions.Add(new RolePermission { RoleId = admin.Id, PermissionId = pseLog.Id });
            }

            await AddRoleIfMissing(PharmacistRole, "Pharmacy counter: sales, returns, reports, products, cash register, PSE logbook",
                new[]
                {
                    Permissions.SaleAccess, Permissions.WholeSaleAccess, Permissions.SaleReturnAccess, Permissions.CustomerLedgerAccess,
                    Permissions.HoldSaleAccess, Permissions.ReportsSales, Permissions.ReportsDaily, Permissions.DashboardAccess,
                    Permissions.ProductsManage, Permissions.CategoriesManage, Permissions.ShiftsManage, Permissions.PseLogView
                }, ct);
            await AddRoleIfMissing(TechnicianRole, "Front counter: sales and returns",
                new[]
                {
                    Permissions.SaleAccess, Permissions.SaleReturnAccess, Permissions.CustomerLedgerAccess,
                    Permissions.HoldSaleAccess, Permissions.DashboardAccess
                }, ct);

            await _db.SaveChangesAsync(ct);
        }

        private async Task AddRoleIfMissing(string name, string description, IEnumerable<string> permissionNames, CancellationToken ct)
        {
            if (await _db.Roles.AnyAsync(r => r.Name == name, ct)) return;

            var role = new Role { Name = name, Description = description, IsSystemRole = true, CreatedDate = DateTime.Now };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);

            var names = permissionNames.ToList();
            var ids = await _db.Permissions.Where(p => names.Contains(p.Name)).Select(p => p.Id).ToListAsync(ct);
            foreach (var id in ids)
                _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });
        }
    }
}
