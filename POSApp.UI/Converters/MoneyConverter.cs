using System.Globalization;
using System.Windows.Data;
using POSApp.UI.Helpers;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Formats a bound amount with the shop's configured currency symbol, replacing the
    /// hardcoded <c>StringFormat='Rs. {0:N2}'</c> that used to be scattered through the views.
    ///
    /// ConverterParameter options:
    ///   (none)  → "Rs. 1,250.00"
    ///   "N0"    → "Rs. 1,250"       (no decimals)
    ///   "-"     → "- Rs. 1,250.00"  (discount rows)
    ///   "+/-"   → "+Rs. 50.00" / "-Rs. 50.00" (cash over/short)
    /// </summary>
    public sealed class MoneyConverter : IValueConverter
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
                "N0"  => Region.MoneyWhole(amount),
                "-"   => "- " + Region.Money(amount),
                "+/-" => Region.MoneySigned(amount),
                _     => Region.Money(amount)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
