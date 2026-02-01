using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InventorySystem.UI.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If text exists and is not empty -> Visible. Otherwise -> Collapsed.
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}