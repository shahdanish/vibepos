using System.Text.Json;

namespace POSApp.Core.Services
{
    /// <summary>
    /// Loads and saves <see cref="RegionSettingsData"/> as <c>region-settings.json</c> in
    /// <see cref="AppPaths.SharedSettingsDirectory"/>, so every cashier on the till shares it.
    /// The file is cached after the first read.
    /// </summary>
    public static class RegionSettingsStore
    {
        public const string FileName = SharedSettingsFiles.Region;

        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        private static RegionSettingsData? _cached;

        /// <summary>Raised after the settings were written to disk (not for <see cref="Apply"/>).</summary>
        public static event Action<RegionSettingsData>? Saved;

        public static string FilePath => SharedSettingsFiles.PathOf(FileName);

        /// <summary>Current settings, loaded from disk on first use and cached thereafter.</summary>
        public static RegionSettingsData Current
        {
            get
            {
                if (_cached != null)
                    return _cached;

                try
                {
                    var path = FilePath;
                    _cached = File.Exists(path) ? Deserialize(File.ReadAllText(path)) : new RegionSettingsData();
                }
                catch
                {
                    // Missing or hand-edited file: fall back to defaults rather than blocking a sale.
                    _cached = new RegionSettingsData();
                }

                return _cached;
            }
        }

        /// <summary>
        /// Reads settings JSON. Properties a file does not contain (written by an older build)
        /// keep their defaults, which reproduce the original Pakistani behaviour.
        /// </summary>
        public static RegionSettingsData Deserialize(string json) =>
            JsonSerializer.Deserialize<RegionSettingsData>(json) ?? new RegionSettingsData();

        public static string Serialize(RegionSettingsData settings) =>
            JsonSerializer.Serialize(settings, WriteOptions);

        /// <summary>Persists the settings and refreshes the cache. Returns false if the file could not be written.</summary>
        public static bool Save(RegionSettingsData settings)
        {
            try
            {
                File.WriteAllText(FilePath, Serialize(settings));
                _cached = settings;
            }
            catch
            {
                return false;
            }

            try { Saved?.Invoke(settings); }
            catch { /* a listener failing must not undo a successful save */ }
            return true;
        }

        /// <summary>
        /// Swaps the in-memory settings without touching disk. Used by the settings screen to
        /// render a preview of unsaved values; always restore the previous settings afterwards.
        /// </summary>
        public static void Apply(RegionSettingsData settings) => _cached = settings;

        /// <summary>Forgets the cached settings so the next read comes from disk.</summary>
        public static void Reload() => _cached = null;
    }
}
