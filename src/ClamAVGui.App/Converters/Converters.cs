using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ClamAVGui.App.Converters;

public class ResourceNameToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current?.Resources.TryGetResource(key, null, out var resource) == true && resource is Geometry geometry)
        {
            return geometry;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class InfectedToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            if (status.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("Infected", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("THREAT", StringComparison.OrdinalIgnoreCase))
            {
                return Brush.Parse("#C42B1C"); // Red
            }
            if (status.EndsWith("OK", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("Clean", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("Complete", StringComparison.OrdinalIgnoreCase))
            {
                return Brush.Parse("#107C10"); // Green
            }
            if (status.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                status.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase))
            {
                return Brush.Parse("#D83B01"); // Orange
            }
        }

        return Brush.Parse("#757575");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class EventTypeToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string eventType)
        {
            if (eventType.Equals("Scan", StringComparison.OrdinalIgnoreCase)) return Brush.Parse("#0067C0"); // Blue
            if (eventType.Contains("Threat", StringComparison.OrdinalIgnoreCase) || eventType.Contains("Failed", StringComparison.OrdinalIgnoreCase)) return Brush.Parse("#C42B1C"); // Red
            if (eventType.Equals("Update", StringComparison.OrdinalIgnoreCase)) return Brush.Parse("#107C10"); // Green
            if (eventType.Contains("Config", StringComparison.OrdinalIgnoreCase)) return Brush.Parse("#8E24AA"); // Purple
        }
        return Brush.Parse("#757575");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class FilePathToShortPathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            return Path.GetFileName(path);
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class FileSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
        return value?.ToString() ?? "0 B";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
