namespace POSApp.Core.Services
{
    /// <summary>Names of the shop-wide settings files kept in <see cref="AppPaths.SharedSettingsDirectory"/>.</summary>
    public static class SharedSettingsFiles
    {
        public const string Region = "region-settings.json";
        public const string ReceiptBranding = "receipt-branding.json";

        public static string PathOf(string fileName) => Path.Combine(AppPaths.SharedSettingsDirectory, fileName);
    }
}
