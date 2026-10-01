using System.Globalization;
using System.Windows.Data;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Formats a bound date in the shop's configured order (day-first, month-first or ISO),
    /// replacing the hardcoded <c>StringFormat=dd/MM/yyyy</c> that used to be scattered
    /// through the views. A US client flips one setting and every date follows.
    ///
    /// ConverterParameter options (day-first examples):
    ///   (none)      → 31/12/2026
    ///   "long"      → 31 Dec 2026
    ///   "short"     → 31/12
    ///   "document"  → 31-Dec-2026
    ///   "time"      → 31/12/2026 09:30 PM
    ///   "shorttime" → 31/12 09:30 PM
    ///   "long24"    → 31 Dec 2026  21:30
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

            var f = Region.Format;
            return (parameter as string) switch
            {
                "long"      => f.Date(d, DateFormat.Long),
                "short"     => f.Date(d, DateFormat.DayMonth),
                "document"  => f.Date(d, DateFormat.Document),
                "time"      => f.DateAndTime(d),
                "shorttime" => f.DateAndTime(d, DateFormat.DayMonth),
                "long24"    => f.Date(d, DateFormat.Long) + "  " + d.ToString("HH:mm", CultureInfo.InvariantCulture),
                "monthyear" => f.Date(d, DateFormat.MonthYear),
                _           => f.Date(d)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
