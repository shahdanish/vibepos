using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;

namespace POSApp.Infrastructure.Services
{
    public sealed class AutoBackupService : IAutoBackupService
    {
        public const string EnabledSettingKey = "Backup.AutoDaily";
        public const int KeepCount = 14;
        private const string Prefix = "posapp_auto_";

        private readonly AppDbContext _db;

        public AutoBackupService(AppDbContext db) => _db = db;

        public string Folder => Path.Combine(AppPaths.DataDirectory, "backups", "auto");

        public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
        {
            var value = await _db.ApplicationSettings.Where(s => s.Key == EnabledSettingKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task SetEnabledAsync(bool enabled, CancellationToken ct = default)
        {
            var row = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == EnabledSettingKey, ct);
            var value = enabled ? "true" : "false";
            if (row == null)
                _db.ApplicationSettings.Add(new ApplicationSetting
                {
                    Key = EnabledSettingKey, Value = value, Description = "Copy the database once a day", CreatedDate = DateTime.Now
                });
            else
            {
                row.Value = value;
                row.ModifiedDate = DateTime.Now;
            }
            await _db.SaveChangesAsync(ct);
        }

        public async Task<string?> RunIfDueAsync(CancellationToken ct = default)
        {
            if (!await IsEnabledAsync(ct)) return null;

            Directory.CreateDirectory(Folder);
            var existing = Directory.GetFiles(Folder, Prefix + "*.db").OrderByDescending(f => f).ToList();
            if (existing.Count > 0 && DateTime.Now - File.GetLastWriteTime(existing[0]) < TimeSpan.FromHours(23))
                return null;

            var path = Path.Combine(Folder, $"{Prefix}{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.db");
            // SQLite's online backup: a consistent snapshot even while the till is in use.
            // Pooling off so the copy is closed straight away (and can be pruned later).
            using (var source = new SqliteConnection(_db.Database.GetConnectionString()))
            using (var target = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await source.OpenAsync(ct);
                await target.OpenAsync(ct);
                source.BackupDatabase(target);
            }

            foreach (var old in Directory.GetFiles(Folder, Prefix + "*.db").OrderByDescending(f => f).Skip(KeepCount))
            {
                try { File.Delete(old); } catch { /* in use; next run tries again */ }
            }
            return path;
        }
    }
}
