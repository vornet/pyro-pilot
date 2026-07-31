using System.Globalization;
using Avalonia.Data.Converters;

namespace PyroPilot.App.Converters;

/// <summary>
/// Maps a burst's normalized lane position (0..1) to a pixel X within the
/// preview panel. The panel's rendered width isn't easily bindable from a
/// nested item template, so this assumes the panel's typical width (see
/// ShowEditorView's preview Border) -- fine for this placeholder 2D preview,
/// which only needs to look roughly centered per lane.
/// </summary>
public sealed class LaneXToPreviewLeftConverter : IValueConverter
{
    public static readonly LaneXToPreviewLeftConverter Instance = new();
    private const double AssumedPreviewWidth = 260;
    private const double HalfBurstWidth = 24;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double laneX ? (laneX * AssumedPreviewWidth) - HalfBurstWidth : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
