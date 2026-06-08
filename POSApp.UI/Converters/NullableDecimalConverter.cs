using System.Globalization;
using System.Windows.Data;

namespace POSApp.UI.Converters
{
    [ValueConversion(typeof(decimal?), typeof(string))]
    public class NullableDecimalConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is decimal d ? d.ToString("G", culture) : string.Empty;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (decimal.TryParse(s, NumberStyles.Any, culture, out decimal result)) return (decimal?)result;
            }
            return null;
        }
    }
}
