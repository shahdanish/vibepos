using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// Keeps a copy of the shop-wide settings files (region and receipt branding) inside the
    /// database, so they travel with every local and cloud backup — the backups only ever copy
    /// <c>posapp.db</c>.
    ///
    /// On a working PC the files stay the source of truth: the copy is refreshed from them on
    /// start-up and after every save. A file is only written back from the copy when it is
    /// missing, which is exactly the "restored a backup onto a new PC" case.
    /// </summary>
    public sealed class SharedSettingsMirror
    {
        /// <summary>Settings file → ApplicationSettings key holding its copy.</summary>
        public static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>
        {
            [SharedSettingsFiles.Region] = "Mirror.RegionSettings",
            [SharedSettingsFiles.ReceiptBranding] = "Mirror.ReceiptBranding"
        };

        private readonly IServiceScopeFactory _scopes;

        public SharedSettingsMirror(IServiceScopeFactory scopes) => _scopes = scopes;

        /// <summary>Brings files and copies in line. Returns the files restored from the database.</summary>
        public async Task<IReadOnlyList<string>> SyncAsync(CancellationToken ct = default)
        {
            var restored = new List<string>();
            using var scope = _scopes.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();

            foreach (var (file, key) in Files)
            {
                var path = SharedSettingsFiles.PathOf(file);
                if (File.Exists(path))
                {
                    var text = await File.ReadAllTextAsync(path, ct);
                    if (await settings.GetSettingAsync(key, ct) != text)
                        await settings.SetSettingAsync(key, text, ct);
                }
                else if (await settings.GetSettingAsync(key, ct) is { Length: > 0 } copy)
                {
                    await File.WriteAllTextAsync(path, copy, ct);
                    restored.Add(file);
                }
            }

            return restored;
        }

        /// <summary>Refreshes the database copy of one settings file after it was saved.</summary>
        public async Task StoreAsync(string fileName, CancellationToken ct = default)
        {
            if (!Files.TryGetValue(fileName, out var key)) return;
            var path = SharedSettingsFiles.PathOf(fileName);
            if (!File.Exists(path)) return;

            using var scope = _scopes.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            await settings.SetSettingAsync(key, await File.ReadAllTextAsync(path, ct), ct);
        }

        /// <summary>
        /// <see cref="StoreAsync"/> for fire-and-forget use from a save handler: never throws. The
        /// next start-up's <see cref="SyncAsync"/> catches up if this one fails.
        /// </summary>
        public async Task StoreQuietlyAsync(string fileName)
        {
            try { await StoreAsync(fileName); }
            catch { /* best effort */ }
        }
    }
}
