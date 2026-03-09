using System.Globalization;
using primeiroApp.Models;

namespace primeiroApp.Converters;

// Cor do label "WHO" no chat
public class SenderColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (MessageSender)(value ?? MessageSender.System) switch
        {
            MessageSender.User   => Color.FromArgb("#4488FF"),
            MessageSender.Claude => Color.FromArgb("#00FF88"),
            MessageSender.Gemini => Color.FromArgb("#FFB800"),
            _                    => Color.FromArgb("#3A4455")
        };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// Cor do corpo da mensagem no chat
public class SenderTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (MessageSender)(value ?? MessageSender.System) switch
        {
            MessageSender.Claude => Color.FromArgb("#D4FFE9"),
            MessageSender.Gemini => Color.FromArgb("#FFF5D4"),
            MessageSender.User   => Color.FromArgb("#C8D4E0"),
            _                    => Color.FromArgb("#3A4455")
        };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// CPU int (0-100) → ProgressBar double (0.0-1.0)
public class IntToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Math.Clamp((value is int i ? i : 0) / 100.0, 0.0, 1.0);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// RAM int % (0-100) → ProgressBar double
public class RamToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Math.Clamp((value is int i ? i : 0) / 100.0, 0.0, 1.0);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// bool online → "ONLINE" / "OFFLINE"
public class OnlineStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "● ONLINE" : "○ OFFLINE";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// bool online → green / red color
public class OnlineColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Color.FromArgb("#00FF88") : Color.FromArgb("#FF3355");
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// task status string → dot color
public class TaskStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "processing" => Color.FromArgb("#FFB800"),
            "done"       => Color.FromArgb("#00FF88"),
            "failed"     => Color.FromArgb("#FF3355"),
            _            => Color.FromArgb("#3A4455")
        };
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// CPU% with threshold param (green below, red above)
public class ThresholdColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is int i ? i : 0;
        var threshold = int.TryParse(parameter?.ToString(), out var t) ? t : 80;
        return v >= threshold ? Color.FromArgb("#FF3355") : Color.FromArgb("#00FF88");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// RAM % (0-100) → color (green < 70, amber 70-85, red > 85)
public class RamColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is int i ? i : 0;
        return v > 85 ? Color.FromArgb("#FF3355") : v > 70 ? Color.FromArgb("#FFB800") : Color.FromArgb("#00FF88");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// Disk free GB → color (green > 50, amber 20-50, red < 20)
public class DiskColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is long l ? l : (value is int i ? i : 0);
        return v < 20 ? Color.FromArgb("#FF3355") : v < 50 ? Color.FromArgb("#FFB800") : Color.FromArgb("#00FF88");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// Pagefile MB → color (green 0, amber 1000-2000, red > 2000)
public class PagefileColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is long l ? l : (value is int i ? i : 0);
        return v > 2000 ? Color.FromArgb("#FF3355") : v > 1000 ? Color.FromArgb("#FFB800") : Color.FromArgb("#00FF88");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// int > 0 → true (for Alerts badge visibility)
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i > 0;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class VoiceButtonColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? Color.FromArgb("#FF3355") : Color.FromArgb("#00FF88");
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
