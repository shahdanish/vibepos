using System.Windows;
using System.Windows.Media;

namespace POSApp.UI.Helpers
{
    /// <summary>An accent colour set: the brand tokens of Themes/DesignSystem.xaml, recoloured.</summary>
    public sealed record AccentPalette(
        string Name,
        Color Primary, Color PrimaryHover,
        Color Accent, Color AccentHover,
        Color Subtle, Color Border, Color OnBrandMuted);

    /// <summary>
    /// Applies the per-Windows-user appearance choices: an accent colour and a density.
    ///
    /// Accent: every view and style reads the brand brushes through DynamicResource, so
    /// replacing them in the application resources recolours open windows immediately.
    /// Density: "Compact" scales each window's content to 90% so more fits on small LED
    /// screens. The defaults (Brown, Comfortable) leave everything exactly as it was.
    /// </summary>
    public static class ThemeManager
    {
        public const string DefaultAccent = "Brown";
        public const string DefaultDensity = "Comfortable";
        public const string CompactDensity = "Compact";
        private const double CompactScale = 0.9;

        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        public static IReadOnlyList<AccentPalette> Accents { get; } = new[]
        {
            new AccentPalette(DefaultAccent,  C("#8B4513"), C("#74390F"), C("#C2692A"), C("#A0522D"), C("#FBF1E8"), C("#EBCFB6"), C("#F3DCC8")),
            new AccentPalette("Pharmacy Teal", C("#0F766E"), C("#115E59"), C("#0D9488"), C("#0F766E"), C("#F0FDFA"), C("#99F6E4"), C("#CCFBF1")),
            new AccentPalette("Clinical Blue", C("#1D4ED8"), C("#1E40AF"), C("#3B82F6"), C("#2563EB"), C("#EFF6FF"), C("#BFDBFE"), C("#DBEAFE")),
            new AccentPalette("Forest Green",  C("#166534"), C("#14532D"), C("#16A34A"), C("#15803D"), C("#F0FDF4"), C("#BBF7D0"), C("#DCFCE7")),
        };

        public static IReadOnlyList<string> Densities { get; } = new[] { DefaultDensity, CompactDensity };

        private static double _scale = 1.0;
        private static bool _initialized;

        /// <summary>Hooks window loading (for density) and applies the saved choices. Call once at start-up.</summary>
        public static void Initialize()
        {
            if (!_initialized)
            {
                EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler((sender, _) => ApplyScale((Window)sender)));
                _initialized = true;
            }

            var settings = SettingsManager.LoadSettings();
            ApplyAccent(settings.Accent);
            ApplyDensity(settings.Density);
        }

        public static AccentPalette FindAccent(string? name) =>
            Accents.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Accents[0];

        /// <summary>Recolours the brand tokens. Unknown names fall back to the default accent.</summary>
        public static void ApplyAccent(string? name)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            var p = FindAccent(name);
            SetBrush(resources, "BrandPrimaryBrush", p.Primary);
            SetBrush(resources, "BrandPrimaryHoverBrush", p.PrimaryHover);
            SetBrush(resources, "BrandAccentBrush", p.Accent);
            SetBrush(resources, "BrandAccentHoverBrush", p.AccentHover);
            SetBrush(resources, "BrandSubtleBrush", p.Subtle);
            SetBrush(resources, "BrandBorderBrush", p.Border);
            SetBrush(resources, "OnBrandMutedBrush", p.OnBrandMuted);
            SetBrush(resources, "InputFocusRingBrush", Color.FromArgb(0x33, p.Primary.R, p.Primary.G, p.Primary.B));
            resources["BrandPrimaryColor"] = p.Primary;
            resources["BrandAccentColor"] = p.Accent;
        }

        /// <summary>"Compact" shrinks window content to 90%; anything else is the original size.</summary>
        public static void ApplyDensity(string? name)
        {
            _scale = string.Equals(name, CompactDensity, StringComparison.OrdinalIgnoreCase) ? CompactScale : 1.0;
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
                ApplyScale(window);
        }

        private static void ApplyScale(Window window)
        {
            if (window.Content is not FrameworkElement content) return;
            if (_scale == 1.0)
            {
                // Original size: only undo a scale this class applied earlier.
                if (content.LayoutTransform is ScaleTransform) content.LayoutTransform = Transform.Identity;
                return;
            }
            content.LayoutTransform = new ScaleTransform(_scale, _scale);
        }

        private static void SetBrush(ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }
    }
}
