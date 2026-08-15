using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PyroPilot.App.Services;
using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.App.ViewModels;

/// <summary>A simple media-first catalog of real firework products.</summary>
public partial class LibraryViewModel : ViewModelBase
{
    private const int MaximumImageBytes = 15 * 1024 * 1024;
    private byte[]? _editPreviewImageData;
    private FireworkEffect _editEffect = new();

    public ObservableCollection<FireworkDefinition> Fireworks { get; } = [];
    public string[] CommonCategories { get; } = ["Cake", "Shell", "Fountain", "Roman Candle", "Mine", "Uncategorized"];

    [ObservableProperty] private FireworkDefinition? _selected;
    [ObservableProperty] private string _editName = "New Firework";
    [ObservableProperty] private string _editCategory = "Uncategorized";
    [ObservableProperty] private int _editDurationMs = 3000;
    [ObservableProperty] private string _editColorHex = "#FF7A00";
    [ObservableProperty] private string? _editDescription;
    [ObservableProperty] private string? _editPreviewImageFileName;
    [ObservableProperty] private Bitmap? _editPreviewImage;
    [ObservableProperty] private string? _editVideoUrl;
    [ObservableProperty] private string? _mediaStatus;

    public bool HasPreviewImage => EditPreviewImage is not null;
    public bool HasVideoUrl => Uri.TryCreate(EditVideoUrl, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public LibraryViewModel()
    {
        foreach (FireworkDefinition firework in FireworkLibraryStore.Load(AppPaths.LibraryFilePath))
            Fireworks.Add(firework);
        New();
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        EditName = "New Firework";
        EditCategory = "Uncategorized";
        EditDurationMs = 3000;
        EditColorHex = "#FF7A00";
        EditDescription = null;
        EditVideoUrl = null;
        _editEffect = new FireworkEffect();
        SetPreviewImage(null, null);
        MediaStatus = null;
    }

    [RelayCommand]
    private void Save()
    {
        if (Selected is null)
        {
            var created = new FireworkDefinition();
            ApplyEdits(created);
            Fireworks.Add(created);
            Selected = created;
        }
        else
        {
            ApplyEdits(Selected);
            int index = Fireworks.IndexOf(Selected);
            if (index >= 0) Fireworks[index] = Selected;
        }

        Persist();
        MediaStatus = "Saved";
    }

    [RelayCommand]
    private void Delete(FireworkDefinition item)
    {
        Fireworks.Remove(item);
        if (ReferenceEquals(Selected, item)) New();
        Persist();
    }

    [RelayCommand]
    private void RemoveImage()
    {
        SetPreviewImage(null, null);
        MediaStatus = "Image removed. Save to keep this change.";
    }

    [RelayCommand]
    private void OpenVideo()
    {
        if (!HasVideoUrl) return;
        Process.Start(new ProcessStartInfo(EditVideoUrl!) { UseShellExecute = true });
    }

    public void ImportImage(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected image no longer exists.", path);
        if (info.Length > MaximumImageBytes) throw new InvalidDataException("Choose an image smaller than 15 MB.");

        byte[] data = File.ReadAllBytes(path);
        SetPreviewImage(data, info.Name);
        MediaStatus = $"Attached {info.Name}. Save to keep it.";
    }

    partial void OnSelectedChanged(FireworkDefinition? value)
    {
        if (value is null) return;
        EditName = value.Name;
        EditCategory = value.Category;
        EditDurationMs = value.DurationMs;
        EditColorHex = value.ColorHex;
        EditDescription = value.Description;
        EditVideoUrl = value.VideoUrl;
        _editEffect = value.Effect;
        SetPreviewImage(value.PreviewImageData, value.PreviewImageFileName);
        MediaStatus = null;
    }

    partial void OnEditVideoUrlChanged(string? value) => OnPropertyChanged(nameof(HasVideoUrl));

    private void ApplyEdits(FireworkDefinition target)
    {
        target.Name = string.IsNullOrWhiteSpace(EditName) ? "Unnamed Firework" : EditName.Trim();
        target.Category = EditCategory;
        target.DurationMs = Math.Max(100, EditDurationMs);
        target.ColorHex = EditColorHex;
        target.Description = EditDescription;
        target.PreviewImageData = _editPreviewImageData?.ToArray();
        target.PreviewImageFileName = EditPreviewImageFileName;
        target.VideoUrl = string.IsNullOrWhiteSpace(EditVideoUrl) ? null : EditVideoUrl.Trim();
        target.Effect = _editEffect;
    }

    private void SetPreviewImage(byte[]? data, string? fileName)
    {
        EditPreviewImage?.Dispose();
        _editPreviewImageData = data?.ToArray();
        EditPreviewImageFileName = fileName;
        EditPreviewImage = data is null ? null : new Bitmap(new MemoryStream(data));
        OnPropertyChanged(nameof(HasPreviewImage));
    }

    private void Persist() => FireworkLibraryStore.Save(AppPaths.LibraryFilePath, Fireworks);
}
