using CommunityToolkit.Mvvm.ComponentModel;

namespace PyroPilot.App.ViewModels;

/// <summary>
/// A short-lived visual "shot fired" marker in the show preview panel. This is
/// a deliberately simple 2D placeholder -- <see cref="ShowEditorViewModel"/>
/// drives it from the same playback clock that will eventually drive a real
/// 3D preview, so that swap doesn't need to touch the playback engine.
/// </summary>
public partial class PreviewBurstViewModel : ViewModelBase
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMilliseconds(900);
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;

    /// <summary>Horizontal position in the preview surface, 0..1.</summary>
    public required double LaneX { get; init; }

    public required string ColorHex { get; init; }

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private double _scale = 0.2;

    /// <returns>True once the burst has finished animating and should be removed.</returns>
    public bool Tick()
    {
        double t = (DateTime.UtcNow - _startedAtUtc).TotalMilliseconds / Lifetime.TotalMilliseconds;
        if (t >= 1.0)
        {
            Opacity = 0;
            return true;
        }

        Opacity = 1.0 - t;
        Scale = 0.2 + t * 1.3;
        return false;
    }
}
