using CommunityToolkit.Mvvm.ComponentModel;

namespace PyroPilot.App.ViewModels;

public partial class PortButtonViewModel(int number) : ViewModelBase
{
    public int Number { get; } = number;

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private DateTimeOffset? _lastFiredUtc;
}
