using CommunityToolkit.Mvvm.ComponentModel;

namespace PyroPilot.App.ViewModels;

public partial class PortButtonViewModel(int number) : ViewModelBase
{
    public int Number { get; } = number;

    [ObservableProperty]
    private bool _isArmed;

    /// <summary>
    /// Whether this port may be armed. It starts enabled so protocols whose
    /// continuity layout is not yet known retain their previous behavior.
    /// A decoded mesh status updates it to the actual fuse continuity state.
    /// </summary>
    [ObservableProperty]
    private bool _isFuseConnected = true;

    [ObservableProperty]
    private DateTimeOffset? _lastFiredUtc;
}
