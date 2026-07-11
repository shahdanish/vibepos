namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// Full-database disaster-recovery service. Unlike <see cref="ISyncService"/>
    /// (which mirrors a handful of tables to Firestore for dashboard viewing), this
    /// service uploads the ENTIRE local SQLite database file (compressed + chunked)
    /// to Firebase and can download it back to fully restore a client after total
    /// data loss. It captures every table with 100% fidelity, including passwords.
    /// </summary>
    public interface ICloudBackupService
    {
        /// <summary>
        /// Initialize the Firestore connection using the same credentials file the
        /// sync service uses. Safe to call once at startup; if credentials are missing
        /// the app keeps working offline and backup/restore simply skip.
        /// </summary>
        void Initialize(string? credentialsPath = null, string? projectId = null);

        /// <summary>
        /// Snapshot the local database and upload it to Firebase as a new cloud backup.
        /// </summary>
        Task<CloudBackupResult> BackupToCloudAsync(CancellationToken ct = default);

        /// <summary>
        /// Download the latest cloud backup and replace the local database with it.
        /// The current database is copied to posapp.db.prerestore first as a safety net.
        /// WARNING: this overwrites ALL current local data.
        /// </summary>
        Task<CloudBackupResult> RestoreFromCloudAsync(CancellationToken ct = default);

        /// <summary>
        /// Read metadata about the most recent cloud backup (for the confirmation dialog),
        /// or <c>null</c> if none exists / offline / credentials missing.
        /// </summary>
        Task<CloudSnapshotInfo?> GetLatestSnapshotInfoAsync(CancellationToken ct = default);
    }

    /// <summary>Outcome of a cloud backup or restore operation. Mirrors <see cref="SyncResult"/>.</summary>
    public sealed class CloudBackupResult
    {
        public bool Success { get; init; }
        /// <summary>True when this result is from a restore (vs a backup).</summary>
        public bool IsRestore { get; init; }
        /// <summary>Uncompressed database size in bytes.</summary>
        public long SizeBytes { get; init; }
        /// <summary>Number of Firestore chunk documents.</summary>
        public int ChunkCount { get; init; }
        /// <summary>Timestamp of the snapshot involved (upload time for backup, snapshot time for restore).</summary>
        public DateTime? SnapshotTime { get; init; }
        public string? ErrorMessage { get; init; }
        /// <summary>Set when the operation was skipped (offline, no credentials, schema mismatch, etc.).</summary>
        public string? SkipReason { get; init; }
        public bool WasSkipped => SkipReason is not null;
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }

    /// <summary>Lightweight metadata about a stored cloud backup.</summary>
    public sealed class CloudSnapshotInfo
    {
        public DateTime SnapshotTime { get; init; }
        public long SizeBytes { get; init; }
        public int ChunkCount { get; init; }
        public string SchemaVersion { get; init; } = string.Empty;
        public string AppVersion { get; init; } = string.Empty;
    }
}
