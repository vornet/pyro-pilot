using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PyroPilot.App.ViewModels;

namespace PyroPilot.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    private async void OnChooseImageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm) return;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose Firework Image",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images")
            {
                Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"],
                MimeTypes = ["image/*"],
            }],
        });

        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        try { vm.ImportImage(path); }
        catch (Exception ex) { vm.MediaStatus = $"Couldn't load image: {ex.Message}"; }
    }
}
