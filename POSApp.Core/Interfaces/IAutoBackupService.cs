namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// A daily copy of the database kept beside it (the newest <c>KeepCount</c> are kept), so a
    /// shop without cloud backup can still get back yesterday's data.
    /// </summary>
    public interface IAutoBackupService
    {
        /// <summary>Where the automatic copies are written.</summary>
        string Folder { get; }

        Task<bool> IsEnabledAsync(CancellationToken ct = default);

        Task SetEnabledAsync(bool enabled, CancellationToken ct = default);

        /// <summary>Makes a copy when switched on and the newest one is a day old (or missing). Returns its path, or null.</summary>
        Task<string?> RunIfDueAsync(CancellationToken ct = default);
    }
}
