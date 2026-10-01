using System.Globalization;
using System.Windows.Data;
using POSApp.UI.Helpers;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Formats a bound amount WITHOUT a currency symbol, using the shop's separators and decimal
    /// places — for read-only grid columns whose header already names the currency. Replaces
    /// <c>StringFormat=N2</c>, which always used WPF's en-US formatting.
    ///
    /// Not for editable text boxes: those parse input back with en-US rules and keep their
    /// plain StringFormat so typing a price keeps working.
    ///
    /// ConverterParameter options:
    ///   (none)    → 1,250.00  (configured decimal places)
    ///   "compact" → 1,250     (receipt-column decimals: 0 for whole-number shops, 2 with cents)
    ///   "list"    → 23 / 23.5 for whole-number shops, 23.00 / 23.50 otherwise
    /// </summary>
    public sealed class NumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return string.Empty;

            decimal amount;
            try
            {
                amount = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return value.ToString() ?? string.Empty;
            }

            return (parameter as string) switch
            {
                "compact" => Region.Compact(amount),
                "list"    => Region.ListPrice(amount),
                _         => Region.Number(amount)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
