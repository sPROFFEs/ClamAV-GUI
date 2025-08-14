using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClamAVGui.Converters
{
    public class ZeroToNormalWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && stringValue != "0")
            {
                return FontWeights.Bold;
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
