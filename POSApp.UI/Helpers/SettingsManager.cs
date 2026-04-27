using System.Configuration;
using System.IO;
using System.Text.Json;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Manages user preferences and application settings
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "POSApp",
            "settings.json"
        );

        private static UserSettings? _cachedSettings;

        public class UserSettings
        {
            public bool AutoPrint { get; set; } = false;
            public bool UseSmallBillFormat { get; set; } = false;
            public bool AutoAddItem { get; set; } = true;
            public bool ShowPurchasePrice { get; set; } = true;
        }

        static SettingsManager()
        {
            EnsureSettingsDirectoryExists();
        }

        private static void EnsureSettingsDirectoryExists()
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public static UserSettings LoadSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    _cachedSettings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
                else
                {
                    _cachedSettings = new UserSettings();
                }
            }
            catch
            {
                _cachedSettings = new UserSettings();
            }

            return _cachedSettings;
        }

        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                _cachedSettings = settings;
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Silently fail - settings are nice to have but not critical
            }
        }

        public static void SaveSetting(Action<UserSettings> updateAction)
        {
            var settings = LoadSettings();
            updateAction(settings);
            SaveSettings(settings);
        }
    }
}
