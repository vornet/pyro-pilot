using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PyroPilot.App.Services;
using PyroPilot.App.ViewModels;
using PyroPilot.App.Views;

namespace PyroPilot.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppPaths.EnsureDirectoriesExist();

            var registry = new DeviceSessionRegistry();
            var workspace = new ShowWorkspaceService();
            var audio = new AudioPlaybackService();

            var devices = new DevicesViewModel(registry);
            var library = new LibraryViewModel();
            var showEditor = new ShowEditorViewModel(workspace, registry, audio, library, devices);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(devices, library, showEditor),
            };

            desktop.ShutdownRequested += (_, _) => audio.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
