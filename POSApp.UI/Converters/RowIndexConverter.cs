using System.Globalization;
using System.Windows.Data;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// Converts a DataGridRow's zero-based ItemsControl.AlternationIndex into a
    /// one-based display sequence number (Sr#). Because the DataGrid recomputes
    /// AlternationIndex for every row whenever items are added or removed, the
    /// numbering stays correct (1, 2, 3 …) without touching any database id.
    /// </summary>
    public sealed class RowIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int index ? (index + 1).ToString() : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
