using CommunityToolkit.Mvvm.ComponentModel;

namespace PyroPilot.App.ViewModels;

/// <summary>
/// Shared zoom state for the timeline editor. Clips hold a reference to the
/// same instance so their pixel geometry (<see cref="ClipViewModel.LeftPx"/>/
/// <see cref="ClipViewModel.WidthPx"/>) updates when the operator zooms,
/// without every clip needing a binding path back up to the show editor.
/// </summary>
public partial class TimelineScale : ObservableObject
{
    [ObservableProperty]
    private double _pixelsPerMs = 60.0 / 1000.0;
}
