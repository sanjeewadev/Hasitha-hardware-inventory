using System;
using System.Globalization;
using System.Windows.Data;

namespace InventorySystem.UI.Converters // <--- MUST MATCH THIS
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean) return !boolean;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean) return !boolean;
            return false;
        }
    }
}