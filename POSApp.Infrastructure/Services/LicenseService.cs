using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using POSApp.Core.Interfaces;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// Yearly time-limited license enforcement.
    ///
    /// Storage model (tamper-resistant):
    ///  - The license record (install date, expiry, last-run, activation count) is
    ///    serialized to JSON and encrypted with Windows DPAPI (LocalMachine scope).
    ///  - The encrypted blob is written to BOTH the registry (HKCU) and a hidden file
    ///    under %ProgramData%. On every run both sources are read and reconciled
    ///    (earliest install wins, latest expiry/activation wins) so deleting one source
    ///    does not reset the trial — the surviving source re-seeds the deleted one.
    ///  - A monotonic "last run" timestamp detects system-clock rollback.
    ///
    /// Renewal is fully offline. The customer reads out the <see cref="LicenseStatus.ActivationId"/>
    /// shown on the expiry screen; the vendor generates a one-time code with the matching
    /// secret (see LicenseSystem/generate-renewal-code.ps1) and the customer types it in.
    ///
    /// NOTE: Because verification runs on the client, the signing secret ships inside the
    /// app binary. This stops ordinary shop users from bypassing expiry; it is not
    /// protection against a determined reverse-engineer (inherent to any offline scheme).
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public sealed class LicenseService : ILicenseService
    {
        // ---- Vendor-configurable settings -----------------------------------------------

        // ============================ TEST MODE ============================
        // Set TestMode = true to make the license expire after ONE MINUTE instead of a
        // year, so you can watch the whole expire -> generate code -> renew flow quickly.
        //
        // >>> MUST STAY false FOR CUSTOMER BUILDS. <<<
        // A build shipped with TestMode = true expires one minute after the customer's
        // first launch. RepairShortPeriod() below heals installs affected by that.
        private const bool TestMode = false;
        // ===================================================================

        /// <summary>Length of one license period. One year in production, one minute in test mode.</summary>
        private static readonly TimeSpan LicensePeriod =
            TestMode ? TimeSpan.FromMinutes(1) : TimeSpan.FromDays(365);

        /// <summary>How close to expiry the "expiring soon" warning starts showing.</summary>
        private static readonly TimeSpan WarnWithin =
            TestMode ? TimeSpan.Zero : TimeSpan.FromDays(15);

        /// <summary>
        /// Allowance for honest clock differences before a backwards jump is treated as
        /// tampering (handles timezone/DST edits and small corrections).
        /// </summary>
        private static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromDays(2);

        /// <summary>
        /// Contact line shown on the expiry screen.
        /// </summary>
        public string RenewalContactMessage => $"To renew your license, contact: {RenewalWhatsAppNumber}";

        /// <summary>WhatsApp number the customer sends the renewal request to.</summary>
        public string RenewalWhatsAppNumber => "0313-7643443";

        /// <summary>
        /// Step-by-step renewal instructions shown under the contact line, so the customer
        /// knows to send a screenshot of this screen (it contains their Activation ID)
        /// over WhatsApp rather than trying to read the code out over a call.
        /// </summary>
        public string RenewalInstructions =>
            $"Please send a WhatsApp message with a screenshot of this screen to {RenewalWhatsAppNumber}. " +
            "You will receive your renewal code on WhatsApp.";

        /// <summary>
        /// Secret used to sign renewal codes. The vendor tool reads the same value from
        /// LicenseSystem/.secret (untracked) or the POSAPP_RENEWAL_SECRET environment
        /// variable — it is deliberately NOT committed alongside this file any more.
        ///
        /// Rotated v1 -> v2 after the v1 value was exposed in a public repository.
        /// Rotating invalidates renewal codes that were issued but not yet entered;
        /// licences already activated are unaffected, since their expiry is stored on
        /// the machine.
        ///
        /// Note this is a shared secret compiled into the client, so a determined user
        /// can recover it by decompiling the assembly. It raises the cost of forging a
        /// code; it does not make it impossible. Asymmetric signing (public key here,
        /// private key kept by the vendor) is the fix if that ever matters.
        /// </summary>
        private const string RenewalSecret = "CounterPointPOS::cPgOmkdpRsnug3hlnYXot50CQjwdZmFL7fsCVokEZ61wpZeH::renewal-v2";

        // ---- Storage locations ----------------------------------------------------------

        private const string RegistrySubKey = @"Software\ShahJeePOS\License";
        private const string RegistryValueName = "L";

        private static readonly string FileStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ShahJeePOS",
            ".license");

        // ---- Public API -----------------------------------------------------------------

        public LicenseStatus CheckLicense()
        {
            var now = DateTime.UtcNow;
            var record = Load();

            if (record is null)
            {
                // First launch on this machine: start the yearly clock.
                record = new LicenseRecord
                {
                    InstallUtc = now,
                    ExpiryUtc = now.Add(LicensePeriod),
                    LastRunUtc = now,
                    ActivationCount = 0
                };
                Save(record);
                return BuildStatus(record, tampered: false);
            }

            // Clock-rollback detection: real time never runs meaningfully backwards.
            var tampered = now < record.LastRunUtc - ClockRollbackTolerance;

            if (!tampered)
            {
                // Advance the monotonic marker and self-heal any deleted storage source.
                if (now > record.LastRunUtc)
                    record.LastRunUtc = now;
                Save(record);
            }

            return BuildStatus(record, tampered);
        }

        public LicenseStatus? Renew(string renewalCode)
        {
            var now = DateTime.UtcNow;
            var record = Load() ?? new LicenseRecord
            {
                InstallUtc = now,
                ExpiryUtc = now.Add(LicensePeriod),
                LastRunUtc = now,
                ActivationCount = 0
            };

            var expected = ComputeRenewalCode(BuildActivationId(record, record.ActivationCount + 1));
            if (!CodesMatch(renewalCode, expected))
                return null;

            // Extend without losing remaining time: start from the later of now / current expiry.
            var basis = record.ExpiryUtc > now ? record.ExpiryUtc : now;
            record.ExpiryUtc = basis.Add(LicensePeriod);
            record.ActivationCount += 1;
            record.LastRunUtc = now;
            Save(record);

            return BuildStatus(record, tampered: false);
        }

        // ---- Status building ------------------------------------------------------------

        private LicenseStatus BuildStatus(LicenseRecord record, bool tampered)
        {
            var now = DateTime.UtcNow;
            var remaining = record.ExpiryUtc - now;
            var daysRemaining = (int)Math.Max(0, Math.Ceiling(remaining.TotalDays));

            LicenseState state;
            if (tampered)
                state = LicenseState.Tampered;
            else if (now >= record.ExpiryUtc)
                state = LicenseState.Expired;
            else if (remaining <= WarnWithin)
                state = LicenseState.Expiring;
            else
                state = LicenseState.Active;

            return new LicenseStatus
            {
                State = state,
                ExpiryUtc = record.ExpiryUtc,
                InstallUtc = record.InstallUtc,
                DaysRemaining = daysRemaining,
                // The next renewal the customer would need to purchase.
                ActivationId = BuildActivationId(record, record.ActivationCount + 1)
            };
        }

        // ---- Activation id + renewal code -----------------------------------------------

        /// <summary>
        /// Stable per-machine fingerprint the renewal code is bound to. Uses the Windows
        /// MachineGuid (survives app reinstall) with a machine-name fallback.
        /// </summary>
        private static string MachineFingerprint()
        {
            string? guid = null;
            try
            {
                using var key = RegistryKey
                    .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                guid = key?.GetValue("MachineGuid") as string;
            }
            catch { /* fall through to machine name */ }

            if (string.IsNullOrWhiteSpace(guid))
                guid = Environment.MachineName;

            // Short, human-readable 8-char id derived from the fingerprint.
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(guid!));
            return Base32(hash).Substring(0, 8);
        }

        /// <summary>
        /// What the customer reads out to the vendor, e.g. "A1B2C3D4-01". The trailing
        /// number is the renewal being purchased, so each year's code is distinct and a
        /// used code cannot be replayed.
        /// </summary>
        private static string BuildActivationId(LicenseRecord _, int renewalNumber)
            => $"{MachineFingerprint()}-{renewalNumber:D2}";

        /// <summary>Deterministic code the vendor's generator must reproduce for a given activation id.</summary>
        private static string ComputeRenewalCode(string activationId)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(RenewalSecret));
            var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(activationId.ToUpperInvariant()));
            var text = Base32(mac).Substring(0, 16);
            // Group into XXXX-XXXX-XXXX-XXXX for readability over the phone.
            return $"{text.Substring(0, 4)}-{text.Substring(4, 4)}-{text.Substring(8, 4)}-{text.Substring(12, 4)}";
        }

        private static bool CodesMatch(string entered, string expected)
        {
            static string Normalize(string s) => new string(
                (s ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

            var a = Encoding.UTF8.GetBytes(Normalize(entered));
            var b = Encoding.UTF8.GetBytes(Normalize(expected));
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        // ---- Encrypted dual-source persistence ------------------------------------------

        private LicenseRecord? Load()
        {
            var fromRegistry = TryDecrypt(ReadRegistry());
            var fromFile = TryDecrypt(ReadFile());

            if (fromRegistry is null && fromFile is null)
                return null;

            if (fromRegistry is null) return RepairShortPeriod(fromFile!);
            if (fromFile is null) return RepairShortPeriod(fromRegistry);

            // Reconcile the two sources so tampering with one cannot shorten the license.
            return RepairShortPeriod(new LicenseRecord
            {
                InstallUtc = Min(fromRegistry.InstallUtc, fromFile.InstallUtc),
                ExpiryUtc = Max(fromRegistry.ExpiryUtc, fromFile.ExpiryUtc),
                LastRunUtc = Max(fromRegistry.LastRunUtc, fromFile.LastRunUtc),
                ActivationCount = Math.Max(fromRegistry.ActivationCount, fromFile.ActivationCount)
            });
        }

        /// <summary>
        /// Heals records whose expiry is shorter than the licence the customer actually
        /// paid for. This happens when a build accidentally shipped with
        /// <see cref="TestMode"/> enabled: the stored expiry is install + 1 minute, so the
        /// customer is locked out within days of installing.
        ///
        /// The entitlement a record represents is always
        /// install + (renewals applied + 1) full periods, so any stored expiry BELOW that
        /// is raised to it. A legitimate record already sits at or above that value
        /// (renewing early carries remaining time forward), so this never shortens a
        /// licence and never grants extra time to a genuinely expired one.
        /// </summary>
        private static LicenseRecord RepairShortPeriod(LicenseRecord record)
        {
            var entitled = record.InstallUtc + LicensePeriod * (record.ActivationCount + 1);
            if (record.ExpiryUtc < entitled)
                record.ExpiryUtc = entitled;
            return record;
        }

        private void Save(LicenseRecord record)
        {
            var blob = Encrypt(record);
            WriteRegistry(blob);
            WriteFile(blob);
        }

        private static LicenseRecord? TryDecrypt(byte[]? blob)
        {
            if (blob is null || blob.Length == 0) return null;
            try
            {
                var plain = ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.LocalMachine);
                return JsonSerializer.Deserialize<LicenseRecord>(plain);
            }
            catch
            {
                // Corrupted or hand-edited: treat as missing, the other source (if any) wins.
                return null;
            }
        }

        private static byte[] Encrypt(LicenseRecord record)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(record);
            return ProtectedData.Protect(json, Entropy, DataProtectionScope.LocalMachine);
        }

        // Extra entropy so the blob is bound to this app, not just the machine.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ShahJeePOS.License.v1");

        private static byte[]? ReadRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey);
                return key?.GetValue(RegistryValueName) as byte[];
            }
            catch { return null; }
        }

        private static void WriteRegistry(byte[] blob)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey);
                key?.SetValue(RegistryValueName, blob, RegistryValueKind.Binary);
            }
            catch { /* best effort */ }
        }

        private static byte[]? ReadFile()
        {
            try
            {
                return File.Exists(FileStorePath) ? File.ReadAllBytes(FileStorePath) : null;
            }
            catch { return null; }
        }

        private static void WriteFile(byte[] blob)
        {
            try
            {
                var dir = Path.GetDirectoryName(FileStorePath)!;
                Directory.CreateDirectory(dir);

                if (File.Exists(FileStorePath))
                    File.SetAttributes(FileStorePath, FileAttributes.Normal);

                File.WriteAllBytes(FileStorePath, blob);
                File.SetAttributes(FileStorePath, FileAttributes.Hidden);
            }
            catch { /* best effort */ }
        }

        // ---- Helpers --------------------------------------------------------------------

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

        /// <summary>Crockford-style Base32 (no padding) for readable codes/ids.</summary>
        private static string Base32(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var sb = new StringBuilder();
            int buffer = 0, bitsLeft = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(alphabet[(buffer >> bitsLeft) & 31]);
                }
            }
            if (bitsLeft > 0)
                sb.Append(alphabet[(buffer << (5 - bitsLeft)) & 31]);
            return sb.ToString();
        }

        private sealed class LicenseRecord
        {
            public DateTime InstallUtc { get; set; }
            public DateTime ExpiryUtc { get; set; }
            public DateTime LastRunUtc { get; set; }
            public int ActivationCount { get; set; }
        }
    }
}
