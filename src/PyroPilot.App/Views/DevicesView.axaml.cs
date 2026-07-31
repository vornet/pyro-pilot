using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PyroPilot.App.ViewModels;

namespace PyroPilot.App.Views;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
    }

    private void OnRemoveDeviceClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DevicesViewModel viewModel) return;
        if (sender is not Button { DataContext: DeviceRowViewModel row }) return;
        viewModel.RemoveDeviceCommand.Execute(row);
    }

    private void OnPortButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PortButtonViewModel port } button) return;

        StyledElement? current = button.Parent;
        while (current is not null && current.DataContext is not DeviceRowViewModel)
            current = current.Parent;

        if (current?.DataContext is DeviceRowViewModel row)
            row.ArmPortCommand.Execute(port);
    }
}
