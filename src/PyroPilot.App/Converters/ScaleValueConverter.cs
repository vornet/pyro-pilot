using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PyroPilot.App.Converters;

/// <summary>Converts a uniform scale factor (double) into a <see cref="ScaleTransform"/> for a preview burst's grow animation.</summary>
public sealed class ScaleValueConverter : IValueConverter
{
    public static readonly ScaleValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double scale ? new ScaleTransform(scale, scale) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
