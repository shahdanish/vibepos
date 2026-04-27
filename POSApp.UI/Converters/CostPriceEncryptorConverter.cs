using System;
using System.Globalization;
using System.Windows.Data;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Encrypts cost price for display (e.g., 200 becomes "X200Y")
    /// </summary>
    public class CostPriceEncryptorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal costPrice)
            {
                // Simple encryption: wrap in X...Y
                return $"X{costPrice:F2}Y";
            }
            
            if (value is double doubleValue)
            {
                return $"X{doubleValue:F2}Y";
            }
            
            if (value is int intValue)
            {
                return $"X{intValue}Y";
            }
            
            return "X0Y";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for read-only display
            throw new NotImplementedException();
        }
    }
}
