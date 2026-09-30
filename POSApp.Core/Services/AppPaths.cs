namespace POSApp.Core.Services
{
    /// <summary>
    /// Single source of truth for where the app keeps its data.
    ///
    /// Everything writable lives under <c>%LocalAppData%\ShahJeePOS</c>. That folder is always
    /// writable, needs no admin rights, and — when the app runs as an MSIX package — Windows
    /// transparently redirects it into the package's private storage, so the same code works
    /// for the Inno Setup install and the Microsoft Store build. The install folder itself is
    /// read-only under MSIX, which is why nothing may be written next to the .exe any more.
    ///
    /// Set the <c>POSAPP_DATA_DIR</c> environment variable to relocate the data (tests, or a
    /// shop that wants the database on another drive).
    /// </summary>
    public static class AppPaths
    {
        public const string DatabaseFileName = "posapp.db";

        private static string? _dataDirectory;

        /// <summary>Root folder for the database, logs and per-install state.</summary>
        public static string DataDirectory
        {
            get
            {
                if (_dataDirectory != null) return _dataDirectory;

                var overridden = Environment.GetEnvironmentVariable("POSAPP_DATA_DIR");
                var dir = !string.IsNullOrWhiteSpace(overridden)
                    ? overridden
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ShahJeePOS");

                Directory.CreateDirectory(dir);
                return _dataDirectory = dir;
            }
        }

        /// <summary>Full path of the SQLite database.</summary>
        public static string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

        /// <summary>EF Core / SQLite connection string for <see cref="DatabasePath"/>.</summary>
        public static string ConnectionString => $"Data Source={DatabasePath}";

        /// <summary>Folder for diagnostic logs.</summary>
        public static string LogsDirectory
        {
            get
            {
                var dir = Path.Combine(DataDirectory, "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// Folder for shop-wide settings (receipt branding, region). A classic install keeps them
        /// machine-wide under %ProgramData% so every cashier's Windows account shares one
        /// configuration. A packaged (Store) install cannot rely on %ProgramData%, so it keeps
        /// them with the rest of its data (as does a run with POSAPP_DATA_DIR set, so test runs
        /// never touch the real shop's settings).
        /// </summary>
        public static string SharedSettingsDirectory
        {
            get
            {
                var relocated = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSAPP_DATA_DIR"));
                var dir = AppEnvironment.IsPackaged || relocated
                    ? DataDirectory
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "ShahJeePOS");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Path of a file inside <see cref="DataDirectory"/>.</summary>
        public static string DataFile(string fileName) => Path.Combine(DataDirectory, fileName);

        /// <summary>
        /// Copies a database left behind by an older build (which kept <c>posapp.db</c> in the
        /// working directory / next to the .exe) into <see cref="DataDirectory"/>. Runs once:
        /// if a database already exists in the new location nothing is touched. The old files
        /// are copied, never moved, so rolling back to an older build still finds its data.
        /// </summary>
        /// <returns>The legacy path that was migrated, or null when nothing was copied.</returns>
        public static string? MigrateLegacyDatabase(params string[] legacyDirectories)
        {
            if (File.Exists(DatabasePath)) return null;

            var candidates = legacyDirectories.Length > 0
                ? legacyDirectories
                : new[] { Environment.CurrentDirectory, AppContext.BaseDirectory };

            foreach (var dir in candidates.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var legacy = Path.Combine(dir, DatabaseFileName);
                if (!File.Exists(legacy)) continue;
                if (string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase))
                    return null;

                // Copy the WAL/SHM side files too so uncheckpointed writes are not lost.
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                {
                    var src = legacy + suffix;
                    if (File.Exists(src))
                        File.Copy(src, DatabasePath + suffix, overwrite: false);
                }
                return legacy;
            }

            return null;
        }

        /// <summary>
        /// Copies a settings file from its old %ProgramData% location into
        /// <see cref="SharedSettingsDirectory"/> when the two differ and the new one is missing.
        /// </summary>
        public static void MigrateLegacySettingsFile(string fileName)
        {
            var target = Path.Combine(SharedSettingsDirectory, fileName);
            if (File.Exists(target)) return;

            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ShahJeePOS", fileName);
            if (File.Exists(legacy) &&
                !string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(legacy, target, overwrite: false);
            }
        }

        /// <summary>Test hook: forget the cached data directory so POSAPP_DATA_DIR is re-read.</summary>
        public static void ResetForTests() => _dataDirectory = null;
    }
}
