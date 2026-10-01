using System.Globalization;
using System.Windows.Data;

namespace POSApp.UI.Converters
{
    /// <summary>
    /// MultiBinding: [0] an id, [1] a set of ids. True when the set contains the id.
    /// Swap the set for a new instance to make every bound row re-evaluate.
    /// </summary>
    public sealed class ContainsIdConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values.Length >= 2 && values[0] is int id && values[1] is IReadOnlySet<int> ids && ids.Contains(id);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
