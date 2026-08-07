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
    private static readonly TimeSpan ContinuityPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly DeviceSessionRegistry _registry;
    private readonly ITitanFireWifiService _wifi;
    private IDeviceSession? _session;
    private CancellationTokenSource? _continuityPollingCts;
    private Task? _continuityPollingTask;

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

    public DeviceRowViewModel(PairedDevice model, DeviceSessionRegistry registry, ITitanFireWifiService wifi)
    {
        Model = model;
        _registry = registry;
        _wifi = wifi;
        Ports = new ObservableCollection<PortButtonViewModel>(
            Enumerable.Range(1, model.PortCount).Select(n => new PortButtonViewModel(n)));
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsBusy || IsConnected) return;
        IsBusy = true;
        StatusText = "Connecting...";
        IDeviceSession? pendingSession = null;
        try
        {
            if (Model.JoinTitanFireWifi)
            {
                StatusText = $"Joining {TitanFireWifiService.Ssid} Wi-Fi...";
                await _wifi.EnsureConnectedAsync();
                StatusText = "Connecting to device...";
            }

            pendingSession = DeviceSessionFactory.Create(Model);
            await pendingSession.ConnectAsync();
            bool loggedIn = await pendingSession.LoginAsync();
            if (!loggedIn)
            {
                StatusText = "Login failed -- check the device is powered on and the host is on its WiFi network.";
                await pendingSession.DisposeAsync();
                pendingSession = null;
                return;
            }

            _session = pendingSession;
            pendingSession = null;
            IsConnected = true;
            StatusText = "Connected.";
            _registry.Register(Model.Id, _session);

            // Mesh continuity decoding is hardware-verified. Read it once
            // immediately, then keep it current in the background.
            if (Model.Protocol == DeviceProtocol.Mesh)
            {
                try
                {
                    PortContinuityStatus? continuity = await _session.TryReadPortContinuityAsync();
                    if (continuity is not null) ApplyContinuity(continuity);
                }
                catch (Exception ex)
                {
                    StatusText = $"Connected; fuse status unavailable: {ex.Message}";
                }

                StartContinuityPolling(_session);
            }
        }
        catch (Exception ex)
        {
            if (pendingSession is not null) await pendingSession.DisposeAsync();
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
        await StopContinuityPollingAsync();
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
        if (!port.IsFuseConnected) return;

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
            if (ok)
            {
                port.LastFiredUtc = DateTimeOffset.UtcNow;
            }
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

    private void StartContinuityPolling(IDeviceSession session)
    {
        _continuityPollingCts = new CancellationTokenSource();
        _continuityPollingTask = PollContinuityAsync(session, _continuityPollingCts.Token);
    }

    private async Task StopContinuityPollingAsync()
    {
        CancellationTokenSource? cts = _continuityPollingCts;
        Task? task = _continuityPollingTask;
        _continuityPollingCts = null;
        _continuityPollingTask = null;

        if (cts is null) return;
        cts.Cancel();
        try
        {
            if (task is not null) await task;
        }
        catch (OperationCanceledException)
        {
            // Expected when disconnecting.
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task PollContinuityAsync(IDeviceSession session, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ContinuityPollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                PortContinuityStatus? continuity = await session.TryReadPortContinuityAsync(cancellationToken);
                if (continuity is not null) ApplyContinuity(continuity);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A transient status failure leaves the most recent known
                // continuity state intact. The next interval retries.
            }
        }
    }

    private void ApplyContinuity(PortContinuityStatus continuity)
    {
        foreach (PortButtonViewModel port in Ports)
            port.IsFuseConnected = continuity.ConnectedPorts.Contains(port.Number);

        if (ArmedPort is not null && !ArmedPort.IsFuseConnected)
        {
            ArmedPort.IsArmed = false;
            ArmedPort = null;
        }
    }

    private bool CanFireArmedPort() => IsConnected && ArmedPort is not null && !IsBusy;

    partial void OnArmedPortChanged(PortButtonViewModel? value) => FireArmedPortCommand.NotifyCanExecuteChanged();
    partial void OnIsConnectedChanged(bool value)
    {
        FireArmedPortCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        FireArmedPortCommand.NotifyCanExecuteChanged();
    }
}
