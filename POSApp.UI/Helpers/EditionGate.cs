using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Views;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Static entry point for edition checks from view-models and code-behind, in the same
    /// spirit as <see cref="SessionManager"/>/<see cref="PermissionManager"/>.
    ///
    /// When the service is not available (unit tests, design time) every feature counts as
    /// enabled so existing behaviour is unchanged.
    /// </summary>
    public static class EditionGate
    {
        public static IEditionService? Service => App.Services?.GetService<IEditionService>();

        public static AppEdition Edition => Service?.Edition ?? AppEdition.Direct;

        public static bool IsEnabled(AppFeature feature) => Service?.IsEnabled(feature) ?? true;

        public static bool IsInBuild(AppFeature feature) => Service?.IsInBuild(feature) ?? true;

        public static Visibility VisibleIfInBuild(AppFeature feature)
            => IsInBuild(feature) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Returns true when <paramref name="feature"/> may be used. If it needs the Pro upgrade,
        /// shows the upgrade dialog first and returns true only if the user bought it.
        /// </summary>
        public static bool Require(AppFeature feature)
        {
            var service = Service;
            if (service == null || service.IsEnabled(feature)) return true;
            if (!service.IsInBuild(feature)) return false;

            var dialog = new UpgradeWindow(service, feature)
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow
            };
            dialog.ShowDialog();
            return service.IsEnabled(feature);
        }

        /// <summary>Opens the upgrade dialog without a specific feature in mind (header button).</summary>
        public static void ShowUpgrade()
        {
            var service = Service;
            if (service == null || service.Edition != AppEdition.StoreLite) return;
            new UpgradeWindow(service, null) { Owner = Application.Current?.MainWindow }.ShowDialog();
        }
    }
}
