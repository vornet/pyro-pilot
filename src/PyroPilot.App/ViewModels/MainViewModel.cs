using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PyroPilot.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public DevicesViewModel Devices { get; }
    public LibraryViewModel Library { get; }
    public ShowEditorViewModel ShowEditor { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainViewModel(DevicesViewModel devices, LibraryViewModel library, ShowEditorViewModel showEditor)
    {
        Devices = devices;
        Library = library;
        ShowEditor = showEditor;
        _currentPage = showEditor;
    }

    [RelayCommand]
    private void GoToShowEditor() => CurrentPage = ShowEditor;

    [RelayCommand]
    private void GoToLibrary() => CurrentPage = Library;

    [RelayCommand]
    private void GoToDevices() => CurrentPage = Devices;
}
