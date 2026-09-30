using POSApp.Core.Services;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// The product's own name, as opposed to the shop's name (<see cref="ReceiptBranding"/>).
    /// The Microsoft Store edition is sold as "Swifttill"; the direct edition keeps the neutral
    /// "POS System" so each client's copy shows only their shop.
    /// </summary>
    public static class ProductBranding
    {
        public const string StoreProductName = "Swifttill";

        public static string Name =>
            EditionPolicy.IsStore(EditionGate.Edition) ? StoreProductName : "POS System";
    }
}
