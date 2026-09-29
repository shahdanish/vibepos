using System.Windows.Markup;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Builds a window title from the shop's own name instead of hardcoding one client's
    /// shop into every view:
    ///
    ///   Title="{helpers:ShopTitle Sale}"   →  "Sale - Ahmed Traders"
    ///   Title="{helpers:ShopTitle}"        →  "Ahmed Traders"
    ///
    /// The name comes from Admin → Receipt Settings, so the same build ships to any shop
    /// in any country. Resolved once when the window is loaded, which is why the settings
    /// screen tells the user to reopen other windows after changing it.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class ShopTitleExtension : MarkupExtension
    {
        public ShopTitleExtension() { }

        public ShopTitleExtension(string screen) => Screen = screen;

        /// <summary>The screen name shown before the shop name, e.g. "Sale". Optional.</summary>
        public string? Screen { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Build(Screen);

        /// <summary>Also callable from code-behind for titles set at runtime.</summary>
        public static string Build(string? screen)
        {
            var shop = ReceiptBranding.Current.StoreName;

            if (string.IsNullOrWhiteSpace(shop))
                return string.IsNullOrWhiteSpace(screen) ? "POS System" : screen!;

            return string.IsNullOrWhiteSpace(screen) ? shop : $"{screen} - {shop}";
        }
    }
}
