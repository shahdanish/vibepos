namespace POSApp.Core.Services
{
    public interface IDatabaseBackupService
    {
        Task<string> CreateBackupAsync(string backupDirectory, CancellationToken ct = default);
        Task RestoreFromBackupAsync(string backupFilePath, CancellationToken ct = default);
        Task<IEnumerable<string>> GetAvailableBackupsAsync(string backupDirectory, CancellationToken ct = default);
    }
}
