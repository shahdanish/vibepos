namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// Overall license state used by the UI to decide whether to allow the app to run.
    /// </summary>
    public enum LicenseState
    {
        /// <summary>License is valid and not close to expiry.</summary>
        Active,

        /// <summary>License is still valid but expires soon (warning window).</summary>
        Expiring,

        /// <summary>The yearly period has elapsed. App must be blocked until renewed.</summary>
        Expired,

        /// <summary>
        /// Tampering detected (system clock rolled back). App is blocked until the
        /// clock is corrected or a valid renewal code is entered.
        /// </summary>
        Tampered
    }

    /// <summary>
    /// Snapshot of the current license, produced by <see cref="ILicenseService.CheckLicense"/>.
    /// </summary>
    public sealed class LicenseStatus
    {
        public LicenseState State { get; init; }

        /// <summary>UTC date the current license period ends.</summary>
        public DateTime ExpiryUtc { get; init; }

        /// <summary>UTC date the software was first installed on this machine.</summary>
        public DateTime InstallUtc { get; init; }

        /// <summary>Whole days remaining until expiry (0 once expired).</summary>
        public int DaysRemaining { get; init; }

        /// <summary>
        /// Machine + renewal identifier the customer reads out to the vendor so a
        /// one-time renewal code can be generated for this exact install.
        /// </summary>
        public string ActivationId { get; init; } = string.Empty;

        public bool IsBlocked => State is LicenseState.Expired or LicenseState.Tampered;
    }

    /// <summary>
    /// Manages the yearly time-limited license: records the first-install date,
    /// enforces the one-year window, detects clock tampering, and applies offline
    /// renewal codes. Storage is tamper-resistant (DPAPI-encrypted, stored in both
    /// the registry and a hidden ProgramData file, cross-checked on every run).
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>
        /// Evaluates the license on startup. On the very first run this records the
        /// install date and starts the one-year period. Safe to call repeatedly.
        /// </summary>
        LicenseStatus CheckLicense();

        /// <summary>
        /// Attempts to apply a renewal code entered by the user. Returns the refreshed
        /// status when the code is valid; returns null when the code is rejected.
        /// </summary>
        LicenseStatus? Renew(string renewalCode);

        /// <summary>
        /// The phone/contact line shown on the expiry screen so customers know how to renew.
        /// </summary>
        string RenewalContactMessage { get; }

        /// <summary>WhatsApp number renewal requests are sent to.</summary>
        string RenewalWhatsAppNumber { get; }

        /// <summary>
        /// Instructions shown on the expiry screen telling the customer to WhatsApp a
        /// screenshot of that screen (which carries their Activation ID) to renew.
        /// </summary>
        string RenewalInstructions { get; }
    }
}
