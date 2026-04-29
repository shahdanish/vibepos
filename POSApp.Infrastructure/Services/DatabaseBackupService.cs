using Microsoft.Data.Sqlite;
using POSApp.Core.Services;

namespace POSApp.Infrastructure.Services
{
    public sealed class DatabaseBackupService : IDatabaseBackupService
    {
        private readonly string _dbPath = "posapp.db";

        public async Task<string> CreateBackupAsync(string backupDirectory, CancellationToken ct = default)
        {
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDirectory, $"posapp_backup_{timestamp}.db");

            // Create a proper SQLite backup
            using (var sourceConnection = new SqliteConnection($"Data Source={_dbPath}"))
            using (var backupConnection = new SqliteConnection($"Data Source={backupPath}"))
            {
                await sourceConnection.OpenAsync(ct);
                await backupConnection.OpenAsync(ct);

                sourceConnection.BackupDatabase(backupConnection);
            }

            return backupPath;
        }

        public async Task RestoreFromBackupAsync(string backupFilePath, CancellationToken ct = default)
        {
            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException("Backup file not found.", backupFilePath);
            }

            // Create a proper SQLite restore
            using (var sourceConnection = new SqliteConnection($"Data Source={backupFilePath}"))
            using (var backupConnection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await sourceConnection.OpenAsync(ct);
                await backupConnection.OpenAsync(ct);

                sourceConnection.BackupDatabase(backupConnection);
            }
        }

        public async Task<IEnumerable<string>> GetAvailableBackupsAsync(string backupDirectory, CancellationToken ct = default)
        {
            if (!Directory.Exists(backupDirectory))
            {
                return Enumerable.Empty<string>();
            }

            var backups = await Task.Run(() =>
            {
                return Directory.GetFiles(backupDirectory, "posapp_backup_*.db")
                    .OrderByDescending(f => f)
                    .ToList();
            }, ct);

            return backups;
        }
    }
}
