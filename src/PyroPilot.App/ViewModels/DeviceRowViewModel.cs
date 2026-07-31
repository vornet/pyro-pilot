using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PyroPilot.App.Services;
using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

/// <summary>
/// One paired device row on the Devices screen: connection lifecycle plus an
/// arm-then-fire port grid (click a port to arm it, then confirm with the
/// separate "Fire" action -- a deliberate two-step interaction so a stray
/// click can't send a live fire command to real hardware).
/// </summary>
public partial class DeviceRowViewModel : ViewModelBase
{
    private readonly DeviceSessionRegistry _registry;
    private IDeviceSession? _session;

    public PairedDevice Model { get; }
    public ObservableCollection<PortButtonViewModel> Ports { get; }
    public string DisplayName => $"{Model.Nickname} ({Model.Protocol}, {Model.Host}:{Model.Port})";

    [ObservableProperty]
    private string _statusText = "Not connected";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private PortButtonViewModel? _armedPort;

    public DeviceRowViewModel(PairedDevice model, DeviceSessionRegistry registry)
    {
        Model = model;
        _registry = registry;
        Ports = new ObservableCollection<PortButtonViewModel>(
            Enumerable.Range(1, model.PortCount).Select(n => new PortButtonViewModel(n)));
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsBusy || IsConnected) return;
        IsBusy = true;
        StatusText = "Connecting...";
        try
        {
            IDeviceSession session = DeviceSessionFactory.Create(Model);
            await session.ConnectAsync();
            bool loggedIn = await session.LoginAsync();
            if (!loggedIn)
            {
                StatusText = "Login failed -- check the device is powered on and the host is on its WiFi network.";
                await session.DisposeAsync();
                return;
            }

            _session = session;
            IsConnected = true;
            StatusText = "Connected.";
            _registry.Register(Model.Id, session);
        }
        catch (Exception ex)
        {
            StatusText = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        _registry.Unregister(Model.Id);
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }

        IsConnected = false;
        StatusText = "Not connected";
        if (ArmedPort is not null) ArmedPort.IsArmed = false;
        ArmedPort = null;
    }

    [RelayCommand]
    private void ArmPort(PortButtonViewModel port)
    {
        if (ArmedPort == port)
        {
            port.IsArmed = false;
            ArmedPort = null;
            return;
        }

        if (ArmedPort is not null) ArmedPort.IsArmed = false;
        port.IsArmed = true;
        ArmedPort = port;
    }

    [RelayCommand(CanExecute = nameof(CanFireArmedPort))]
    private async Task FireArmedPortAsync()
    {
        if (_session is null || ArmedPort is null) return;
        PortButtonViewModel port = ArmedPort;

        IsBusy = true;
        try
        {
            bool ok = await _session.ManualFireAsync(port.Number);
            StatusText = ok ? $"Fired port {port.Number}." : $"Port {port.Number} FAILED to fire.";
            if (ok) port.LastFiredUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            StatusText = $"Fire failed: {ex.Message}";
        }
        finally
        {
            port.IsArmed = false;
            ArmedPort = null;
            IsBusy = false;
        }
    }

    private bool CanFireArmedPort() => IsConnected && ArmedPort is not null && !IsBusy;

    partial void OnArmedPortChanged(PortButtonViewModel? value) => FireArmedPortCommand.NotifyCanExecuteChanged();
    partial void OnIsConnectedChanged(bool value) => FireArmedPortCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => FireArmedPortCommand.NotifyCanExecuteChanged();
}
