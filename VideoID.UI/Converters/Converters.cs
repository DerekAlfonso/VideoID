using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoID.Core.Models;

namespace VideoID.UI.Converters;

/// <summary>byte[] (JPEG) → BitmapImage for binding to Image.Source.</summary>
[ValueConversion(typeof(byte[]), typeof(ImageSource))]
public sealed class ByteArrayToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(bytes);
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool → green / red SolidColorBrush (GPU availability indicator).</summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))  // green
            : new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)); // grey

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool → !bool</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>!bool → Visibility</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>VideoProcessingState → status dot colour.</summary>
[ValueConversion(typeof(VideoProcessingState), typeof(Brush))]
public sealed class StateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (VideoProcessingState)(value ?? VideoProcessingState.Pending) switch
        {
            VideoProcessingState.Pending   => new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
            VideoProcessingState.Scanning  => new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
            VideoProcessingState.Completed => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            VideoProcessingState.Failed    => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
            _                              => Brushes.Transparent
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
