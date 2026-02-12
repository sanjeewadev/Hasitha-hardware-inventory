using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InventorySystem.UI.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the object is null (e.g. No Parent Category), Hide it. 
            // If it exists, Show it.
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}