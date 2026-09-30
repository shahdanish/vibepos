namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// One-time setup for a fresh Store install: replaces the demo logins shipped in the seed
    /// data with the shop owner's own account.
    /// </summary>
    public interface IFirstRunSetupService
    {
        /// <summary>True once <see cref="CompleteAsync"/> has run on this database.</summary>
        Task<bool> IsCompleteAsync(CancellationToken ct = default);

        Task CompleteAsync(FirstRunSetupRequest request, CancellationToken ct = default);
    }

    public sealed record FirstRunSetupRequest(
        string AdminUsername,
        string AdminPassword,
        bool KeepSampleProducts);
}
