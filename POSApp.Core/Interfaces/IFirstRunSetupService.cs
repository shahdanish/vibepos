namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// One-time setup for a new install: replaces the demo logins shipped in the seed data
    /// with the shop owner's own account, and optionally loads sample data.
    /// </summary>
    public interface IFirstRunSetupService
    {
        /// <summary>True once <see cref="CompleteAsync"/> has run on this database.</summary>
        Task<bool> IsCompleteAsync(CancellationToken ct = default);

        /// <summary>
        /// Records, once per database, whether this version created it (<see cref="InstallOrigin.New"/>)
        /// or found it already in use (<see cref="InstallOrigin.Upgraded"/>), and returns the record.
        /// </summary>
        /// <param name="databaseExistedAtStart">The database had migrations applied before this start.</param>
        Task<InstallOriginResult> RecordInstallOriginAsync(bool databaseExistedAtStart, CancellationToken ct = default);

        Task<FirstRunSetupResult> CompleteAsync(FirstRunSetupRequest request, CancellationToken ct = default);
    }

    /// <summary>Whether a database was created by a build with the US defaults or predates them.</summary>
    public enum InstallOrigin
    {
        /// <summary>Created by this version or later: US defaults and first-run setup apply.</summary>
        New,

        /// <summary>
        /// In use before the US defaults (for example the Pakistani tills): keeps the behaviour it
        /// had, with no setup wizard on the direct edition.
        /// </summary>
        Upgraded
    }

    /// <param name="FirstSeen">True on the start that recorded it (the one-off upgrade steps run then).</param>
    public sealed record InstallOriginResult(InstallOrigin Origin, bool FirstSeen);

    public sealed record FirstRunSetupRequest(
        string AdminUsername,
        string AdminPassword,
        bool LoadSampleData);

    /// <summary>A sample staff login created with the sample data.</summary>
    public sealed record SampleLogin(string Username, string Password, string Role);

    /// <summary>What setup added besides the owner's account.</summary>
    public sealed record FirstRunSetupResult(int SampleProducts, int SampleCustomers, IReadOnlyList<SampleLogin> SampleLogins)
    {
        public static FirstRunSetupResult NoSamples { get; } = new(0, 0, Array.Empty<SampleLogin>());
    }
}
