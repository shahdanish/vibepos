using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Services;
using POSApp.Data;

namespace POSApp.Infrastructure.Services
{
    /// <summary>What <see cref="DatabaseMigrator.MigrateWithBackup"/> did.</summary>
    /// <param name="Applied">Migrations applied by this run (empty when the database was up to date).</param>
    /// <param name="BackupPath">The copy taken before migrating, or null when there was nothing to back up.</param>
    public sealed record MigrationOutcome(IReadOnlyList<string> Applied, string? BackupPath);

    /// <summary>
    /// Applies pending EF Core migrations at startup, first copying the existing database aside
    /// so an upgrade can always be rolled back by hand. Runs nothing when the schema is current,
    /// so the copy is made once per upgrade, not on every start. A fresh install (no migrations
    /// applied yet) has nothing to protect and is not copied.
    /// </summary>
    public static class DatabaseMigrator
    {
        public const int BackupsToKeep = 5;

        /// <summary>Where pre-migration copies are kept (kept apart from the user's own backups).</summary>
        public static string BackupDirectory => Path.Combine(AppPaths.DataDirectory, "backups", "pre-migration");

        public static MigrationOutcome MigrateWithBackup(AppDbContext db)
        {
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count == 0)
                return new MigrationOutcome(Array.Empty<string>(), null);

            var applied = db.Database.GetAppliedMigrations().ToList();
            var backup = applied.Count > 0 ? BackUp(db, applied[^1]) : null;

            db.Database.Migrate();

            Log($"Applied {string.Join(", ", pending)}" + (backup is null ? " (new database)" : $"; backup: {backup}"));
            return new MigrationOutcome(pending, backup);
        }

        private static string BackUp(AppDbContext db, string fromMigration)
        {
            Directory.CreateDirectory(BackupDirectory);
            var stamp = AppClock.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(BackupDirectory, $"posapp_{fromMigration}_{stamp}.db");

            // SQLite's online backup copies a consistent snapshot, WAL contents included.
            using (var source = new SqliteConnection(db.Database.GetConnectionString()))
            using (var target = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                source.Open();
                target.Open();
                source.BackupDatabase(target);
            }

            Prune();
            return path;
        }

        private static void Prune()
        {
            var stale = new DirectoryInfo(BackupDirectory)
                .GetFiles("posapp_*.db")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(BackupsToKeep);

            foreach (var file in stale)
            {
                try { file.Delete(); }
                catch (IOException) { /* in use — try again on the next upgrade */ }
            }
        }

        private static void Log(string message)
        {
            try
            {
                var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(AppPaths.LogsDirectory, "migrations.log"), line);
            }
            catch
            {
                // Logging must never stop the app from starting.
            }
        }
    }
}
