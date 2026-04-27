using System;
using System.Globalization;
using System.Windows.Data;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Encrypts total purchase cost for staff display (e.g., 200 becomes "XX200.00YY")
    /// </summary>
    public class PurchaseCostEncryptorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal purchasePrice)
            {
                // Format: XX{amount}YY for better visibility
                return $"XX{purchasePrice:F2}YY";
            }
            
            if (value is double doubleValue)
            {
                return $"XX{doubleValue:F2}YY";
            }
            
            if (value is int intValue)
            {
                return $"XX{intValue}.00YY";
            }
            
            return "XX0.00YY";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for read-only display
            throw new NotImplementedException();
        }
    }
}
