namespace POSApp.Core.Interfaces
{
    public interface ISyncService
    {
        /// <summary>
        /// Log a local entity change for later sync to Firebase.
        /// Called automatically by SyncLogInterceptor — no manual calls needed.
        /// </summary>
        Task LogChangeAsync(string entityType, int entityId, string operation, CancellationToken ct = default);

        /// <summary>
        /// Push all pending (unsynced) changes to Firebase Firestore.
        /// Safe to call repeatedly — idempotent.
        /// </summary>
        Task SyncPendingChangesAsync(CancellationToken ct = default);

        /// <summary>
        /// Whether the device currently has internet connectivity.
        /// </summary>
        bool IsOnline { get; }

        /// <summary>
        /// Raised when connectivity state changes (online/offline).
        /// </summary>
        event EventHandler<bool>? ConnectivityChanged;

        /// <summary>
        /// Number of pending (unsynced) changes in the SyncLog.
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// Raised when a sync cycle completes (success or failure).
        /// </summary>
        event EventHandler<SyncResult>? SyncCompleted;
    }

    public sealed class SyncResult
    {
        public bool Success { get; init; }
        public int PushedCount { get; init; }
        public int FailedCount { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }
}
