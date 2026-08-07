using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PyroPilot.App.Services;
using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.App.ViewModels;

public partial class DevicesViewModel : ViewModelBase
{
    private readonly DeviceSessionRegistry _registry;
    private readonly ITitanFireWifiService _wifi;

    public ObservableCollection<DeviceRowViewModel> Devices { get; } = [];
    public DeviceProtocol[] ProtocolOptions { get; } = Enum.GetValues<DeviceProtocol>();

    [ObservableProperty]
    private string _newNickname = "Front Yard";

    [ObservableProperty]
    private DeviceProtocol _newProtocol = DeviceProtocol.Mesh;

    [ObservableProperty]
    private string _newHost = "192.168.8.1";

    [ObservableProperty]
    private int _newPort = 7008;

    [ObservableProperty]
    private int _newPortCount = 15;

    public DevicesViewModel(DeviceSessionRegistry registry, ITitanFireWifiService wifi)
    {
        _registry = registry;
        _wifi = wifi;
        foreach (PairedDevice device in PairedDeviceStore.Load(AppPaths.DevicesFilePath))
            Devices.Add(CreateRow(device));
    }

    partial void OnNewProtocolChanged(DeviceProtocol value)
    {
        // Nudge host/port to the new protocol's real-device default, but only
        // when they still hold the *other* protocol's default -- don't
        // clobber a value the operator already typed on purpose.
        if (value == DeviceProtocol.Mesh && NewHost == "192.168.4.1")
        {
            NewHost = "192.168.8.1";
            NewPort = 7008;
        }
        else if (value == DeviceProtocol.Single && NewHost == "192.168.8.1")
        {
            NewHost = "192.168.4.1";
            NewPort = 80;
        }
    }

    [RelayCommand]
    private async Task AddDeviceAsync()
    {
        var model = new PairedDevice
        {
            Nickname = string.IsNullOrWhiteSpace(NewNickname) ? "Device" : NewNickname,
            Protocol = NewProtocol,
            Host = NewHost,
            Port = NewPort,
            PortCount = Math.Clamp(NewPortCount, 1, 60),
            JoinTitanFireWifi = NewProtocol == DeviceProtocol.Mesh,
            AutoConnect = true,
        };
        DeviceRowViewModel row = CreateRow(model);
        Devices.Add(row);
        Persist();
        await row.ConnectCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task RemoveDeviceAsync(DeviceRowViewModel row)
    {
        await row.DisconnectCommand.ExecuteAsync(null);
        Devices.Remove(row);
        Persist();
    }

    public async Task AutoConnectAsync()
    {
        foreach (DeviceRowViewModel row in Devices.Where(row => row.Model.AutoConnect))
            await row.ConnectCommand.ExecuteAsync(null);
    }

    private DeviceRowViewModel CreateRow(PairedDevice model) => new(model, _registry, _wifi);

    private void Persist() => PairedDeviceStore.Save(AppPaths.DevicesFilePath, Devices.Select(row => row.Model));
}
