using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WindowsStorageCleaner.Models;

namespace WindowsStorageCleaner.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolVal = value is bool b && b;
        if (parameter is string invert && invert.Equals("invert", StringComparison.OrdinalIgnoreCase))
            boolVal = !boolVal;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public class ThemeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isDark && isDark ? "☀️" : "🌙";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AdminTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isAdmin && isAdmin ? "Administrator" : "Standard";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AdminBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isAdmin && isAdmin
            ? new SolidColorBrush(Color.FromRgb(232, 245, 233))
            : new SolidColorBrush(Color.FromRgb(255, 243, 224));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AdminFgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isAdmin && isAdmin
            ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
            : new SolidColorBrush(Color.FromRgb(230, 81, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                LogLevel.Success => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                _ => new SolidColorBrush(Color.FromRgb(255, 202, 40))
            };
        }
        return new SolidColorBrush(Color.FromRgb(255, 202, 40));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToIndentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool indent && indent ? new Thickness(20, 0, 0, 0) : new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
