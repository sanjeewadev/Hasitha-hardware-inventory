using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace InventorySystem.UI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isError && isError)
            {
                // Return RED for Error
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            }
            // Return GREEN for Success
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}