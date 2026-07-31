using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PyroPilot.App.Services;
using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

public partial class DevicesViewModel : ViewModelBase
{
    private readonly DeviceSessionRegistry _registry;

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

    public DevicesViewModel(DeviceSessionRegistry registry)
    {
        _registry = registry;
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
    private void AddDevice()
    {
        var model = new PairedDevice
        {
            Nickname = string.IsNullOrWhiteSpace(NewNickname) ? "Device" : NewNickname,
            Protocol = NewProtocol,
            Host = NewHost,
            Port = NewPort,
            PortCount = Math.Clamp(NewPortCount, 1, 60),
        };
        Devices.Add(new DeviceRowViewModel(model, _registry));
    }

    [RelayCommand]
    private async Task RemoveDeviceAsync(DeviceRowViewModel row)
    {
        await row.DisconnectCommand.ExecuteAsync(null);
        Devices.Remove(row);
    }
}
