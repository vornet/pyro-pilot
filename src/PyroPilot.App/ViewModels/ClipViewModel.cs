using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

/// <summary>Timeline-editor wrapper over a <see cref="TimelineClip"/>, exposing the pixel geometry the view drags and resizes.</summary>
public abstract class ClipViewModel : ViewModelBase
{
    private readonly TimelineScale _scale;

    protected ClipViewModel(TimelineScale scale)
    {
        _scale = scale;
        _scale.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(LeftPx));
            OnPropertyChanged(nameof(WidthPx));
        };
    }

    public abstract TimelineClip Model { get; }

    public Guid Id => Model.Id;

    public int StartMs
    {
        get => Model.StartMs;
        set
        {
            if (Model.StartMs == value) return;
            Model.StartMs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndMs));
            OnPropertyChanged(nameof(LeftPx));
        }
    }

    public int DurationMs
    {
        get => Model.DurationMs;
        set
        {
            int clamped = Math.Max(100, value);
            if (Model.DurationMs == clamped) return;
            Model.DurationMs = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndMs));
            OnPropertyChanged(nameof(WidthPx));
        }
    }

    public int EndMs => Model.EndMs;

    /// <summary>Horizontal position on the timeline canvas, in pixels.</summary>
    public double LeftPx => StartMs * _scale.PixelsPerMs;

    /// <summary>Clip width on the timeline canvas, in pixels.</summary>
    public double WidthPx => Math.Max(4, DurationMs * _scale.PixelsPerMs);

    public abstract string Label { get; }
    public abstract string ColorHex { get; }
}
