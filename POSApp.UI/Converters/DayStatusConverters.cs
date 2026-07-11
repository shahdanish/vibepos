using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Shared lookup of a calendar day's status from the ViewModel's day-status map.
    /// Inputs: [0] = the day's DateTime (a CalendarDayButton's content), [1] = the
    /// IReadOnlyDictionary&lt;DateOnly, DayCallStatus&gt; exposed by the ViewModel.
    /// </summary>
    internal static class DayStatusLookup
    {
        public static DayCallStatus Resolve(object[] values)
        {
            if (values.Length < 2 || values[0] is not DateTime dt)
                return DayCallStatus.None;

            if (values[1] is IReadOnlyDictionary<DateOnly, DayCallStatus> map
                && map.TryGetValue(DateOnly.FromDateTime(dt), out var status))
                return status;

            return DayCallStatus.None;
        }
    }

    /// <summary>Visible when the day has scheduled calls with at least one still pending.</summary>
    public sealed class PendingDotVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => DayStatusLookup.Resolve(values) == DayCallStatus.HasPending ? Visibility.Visible : Visibility.Collapsed;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Visible when the day has scheduled calls and all of them are done.</summary>
    public sealed class DoneTickVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => DayStatusLookup.Resolve(values) == DayCallStatus.AllDone ? Visibility.Visible : Visibility.Collapsed;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
