using System;
using System.Globalization;
using System.Windows.Data;
using System.IO;

namespace ClamAVGui.Converters
{
    public class FilePathToShortPathConverter : IValueConverter
    {
        public static FilePathToShortPathConverter Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path)
            {
                return string.Empty;
            }

            int maxLength = 80; // Default max length
            if (parameter is int pLength)
            {
                maxLength = pLength;
            }
            else if (parameter is string sLength && int.TryParse(sLength, out int psLength))
            {
                maxLength = psLength;
            }

            if (path.Length <= maxLength)
            {
                return path;
            }

            try
            {
                string filename = Path.GetFileName(path);
                var directory = Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(directory))
                {
                    return path; // Should not happen with long paths, but as a safeguard.
                }

                int remainingLength = maxLength - filename.Length - 3; // -3 for "..."
                if (remainingLength < 1)
                {
                    // Filename itself is too long, just truncate it
                    return "..." + path.Substring(path.Length - maxLength + 3);
                }

                return directory.Substring(0, remainingLength) + "..." + filename;
            }
            catch (ArgumentException)
            {
                // Path contains invalid characters.
                return path;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
