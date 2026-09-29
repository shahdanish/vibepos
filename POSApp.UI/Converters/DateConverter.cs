using System.Globalization;
using System.Windows.Data;
using POSApp.UI.Helpers;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Formats a bound date in the shop's configured order (day-first, month-first or ISO),
    /// replacing the hardcoded <c>StringFormat=dd/MM/yyyy</c> that used to be scattered
    /// through the views. A US client flips one setting and every date follows.
    ///
    /// ConverterParameter options:
    ///   (none)    → 31/12/2026
    ///   "long"    → 31 Dec 2026
    ///   "short"   → 31/12
    ///   "time"    → 31/12/2026 09:30 PM
    ///   "shorttime" → 31/12 09:30 PM
    ///   "monthyear" → 12/2026
    /// </summary>
    public sealed class DateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime d)
            {
                if (value is DateTimeOffset o) d = o.LocalDateTime;
                else return value?.ToString() ?? string.Empty;
            }

            var inv = CultureInfo.InvariantCulture;
            return (parameter as string) switch
            {
                "long"      => Region.LongDate(d),
                "short"     => d.ToString(Region.ShortDatePattern, inv),
                "time"      => Region.DateTimeText(d),
                "shorttime" => d.ToString(Region.ShortDatePattern + " hh:mm tt", inv),
                "monthyear" => d.ToString(Region.Current.Dates == DateStyle.Iso ? "yyyy-MM" : "MM/yyyy", inv),
                _           => Region.Date(d)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
