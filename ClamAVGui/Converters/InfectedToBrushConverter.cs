using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ClamAVGui.Converters
{
    public class InfectedToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string statusText)
            {
                if (statusText.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Red;
                }

                if (statusText.Contains("OK", StringComparison.OrdinalIgnoreCase))
                {
                    return Brushes.Green;
                }
            }

            if (value is string stringValue && int.TryParse(stringValue, out int intValue))
            {
                return intValue > 0 ? Brushes.Red : Brushes.Green;
            }
            if (value is int intVal)
            {
                return intVal > 0 ? Brushes.Red : Brushes.Green;
            }
            return Brushes.Gray; // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
