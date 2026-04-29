using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POSApp.UI.Converters
{
    public sealed class IsDeletedToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDeleted)
            {
                return isDeleted ? new SolidColorBrush(Color.FromRgb(244, 67, 54)) :
                                   new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            return new SolidColorBrush(Color.FromRgb(76, 175, 80));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class IsDeletedToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDeleted)
            {
                return isDeleted ? "DELETED" : "ACTIVE";
            }
            return "ACTIVE";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
