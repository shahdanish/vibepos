using POSApp.Core.Entities;
using POSApp.Core.Interfaces;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Helper class for stock alert functionality
    /// </summary>
    public static class StockAlertHelper
    {
        /// <summary>
        /// Check if a product has low stock
        /// </summary>
        public static bool IsLowStock(Product product)
        {
            return product.Stock <= product.MinStockThreshold;
        }

        /// <summary>
        /// Check if a product is out of stock
        /// </summary>
        public static bool IsOutOfStock(Product product)
        {
            return product.Stock <= 0;
        }

        /// <summary>
        /// Get alert message for a product
        /// </summary>
        public static string GetAlertMessage(Product product)
        {
            if (IsOutOfStock(product))
            {
                return $"⚠️ OUT OF STOCK: {product.ProductName}";
            }
            else if (IsLowStock(product))
            {
                return $"⚠️ LOW STOCK: {product.ProductName} - Only {product.Stock} remaining (Threshold: {product.MinStockThreshold})";
            }
            return string.Empty;
        }

        /// <summary>
        /// Get alert level (0=None, 1=Low, 2=Critical)
        /// </summary>
        public static int GetAlertLevel(Product product)
        {
            if (product.Stock <= 0) return 2; // Critical
            if (product.Stock <= product.MinStockThreshold) return 1; // Low
            return 0; // None
        }
    }
}
