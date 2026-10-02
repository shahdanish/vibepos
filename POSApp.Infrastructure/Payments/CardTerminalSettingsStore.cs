using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Data;

namespace POSApp.Infrastructure.Payments
{
    /// <summary>
    /// Card reader set-up in ApplicationSettings. The API key is encrypted for this computer
    /// (Windows DPAPI): a database backup restored elsewhere carries no usable key, and the
    /// shop simply enters it again there.
    /// </summary>
    public sealed class CardTerminalSettingsStore : ICardTerminalSettingsStore
    {
        public const string ProviderKey = "Payments.Provider";
        public const string ReaderKey = "Payments.ReaderId";
        public const string ApiKeyKey = "Payments.ApiKey";

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Swifttill.CardTerminal.v1");

        private readonly AppDbContext _db;

        public CardTerminalSettingsStore(AppDbContext db) => _db = db;

        public async Task<CardTerminalSettings> GetAsync(CancellationToken ct = default)
        {
            var rows = await _db.ApplicationSettings.AsNoTracking()
                .Where(s => s.Key == ProviderKey || s.Key == ReaderKey || s.Key == ApiKeyKey)
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
            var provider = rows.GetValueOrDefault(ProviderKey) ?? CardTerminalSettings.None;
            return new CardTerminalSettings(provider, Unprotect(rows.GetValueOrDefault(ApiKeyKey)), Blank(rows.GetValueOrDefault(ReaderKey)));
        }

        public async Task SaveAsync(CardTerminalSettings settings, CancellationToken ct = default)
        {
            await Set(ProviderKey, settings.Provider, ct);
            await Set(ReaderKey, settings.ReaderId?.Trim() ?? string.Empty, ct);
            await Set(ApiKeyKey, Protect(settings.ApiKey?.Trim()), ct);
            await _db.SaveChangesAsync(ct);
        }

        private async Task Set(string key, string value, CancellationToken ct)
        {
            var row = await _db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (row == null)
                _db.ApplicationSettings.Add(new ApplicationSetting { Key = key, Value = value, Description = "Card reader set-up", CreatedDate = DateTime.Now });
            else if (row.Value != value)
            {
                row.Value = value;
                row.ModifiedDate = DateTime.Now;
            }
        }

        private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

        public static string Protect(string? secret)
        {
            if (string.IsNullOrEmpty(secret)) return string.Empty;
            var blob = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(blob);
        }

        public static string? Unprotect(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.LocalMachine));
            }
            catch
            {
                return null;   // from another PC (restored backup) or damaged: enter the key again
            }
        }
    }

    /// <summary>Builds the configured card reader, or none.</summary>
    public sealed class CardTerminalFactory
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

        private readonly ICardTerminalSettingsStore _store;

        public CardTerminalFactory(ICardTerminalSettingsStore store) => _store = store;

        public async Task<ICardTerminal?> CreateAsync(CancellationToken ct = default)
        {
            var s = await _store.GetAsync(ct);
            return s.IsStripe ? new StripeTerminal(Http, s.ApiKey!, s.ReaderId!, apiBase: TestApiBase) : null;
        }

        /// <summary>A Stripe client for the set-up screen (listing readers, test reader).</summary>
        public static StripeTerminal ForSetup(string apiKey, string readerId = "") => new(Http, apiKey, readerId, apiBase: TestApiBase);

        /// <summary>
        /// Debug builds only: POSAPP_STRIPE_API_BASE points the reader at a local stand-in for
        /// Stripe, so the whole card flow can be tried without an account. Release builds always use Stripe.
        /// </summary>
        private static string? TestApiBase =>
#if DEBUG
            Environment.GetEnvironmentVariable("POSAPP_STRIPE_API_BASE") is { Length: > 0 } b ? b.TrimEnd('/') + "/" : null;
#else
            null;
#endif
    }
}
