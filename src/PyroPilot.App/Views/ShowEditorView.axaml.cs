using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PyroPilot.App.ViewModels;
using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.App.Views;

public partial class ShowEditorView : UserControl
{
    private static readonly DataFormat<FireworkDefinition> FireworkDragFormat =
        DataFormat.CreateInProcessFormat<FireworkDefinition>("pyropilot.firework");

    // Clip selection / move-drag state.
    private Border? _selectedClipBorder;
    private Border? _draggingClipBorder;
    private Canvas? _dragCanvas;
    private TrackViewModel? _dragTrack;
    private Point _dragStartPointerPos;
    private int _dragOriginalStartMs;

    // Clip resize-drag state.
    private Border? _resizingHandle;
    private ClipViewModel? _resizingClip;
    private Canvas? _resizeCanvas;
    private TrackViewModel? _resizeTrack;
    private double _resizeStartPointerX;
    private int _resizeOriginalDurationMs;

    public ShowEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ShowEditorViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            RebuildRuler(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShowEditorViewModel.RulerSeconds) or nameof(ShowEditorViewModel.TimelineWidthPx) &&
            DataContext is ShowEditorViewModel vm)
        {
            RebuildRuler(vm);
        }
    }

    private void RebuildRuler(ShowEditorViewModel vm)
    {
        RulerCanvas.Children.Clear();
        foreach (int second in vm.RulerSeconds)
        {
            double x = second * 1000 * vm.PixelsPerMs;
            bool major = second % 5 == 0;

            var tick = new Rectangle { Width = 1, Height = major ? 14 : 6, Fill = Brushes.Gray };
            Canvas.SetLeft(tick, x);
            Canvas.SetTop(tick, 14);
            RulerCanvas.Children.Add(tick);

            if (major)
            {
                var label = new TextBlock { Text = $"{second / 60}:{second % 60:00}", FontSize = 10, Foreground = Brushes.Gray };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 0);
                RulerCanvas.Children.Add(label);
            }
        }
    }

    // --- Toolbar: New / Open / Save ---

    private void OnNewShowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShowEditorViewModel vm) vm.NewShowCommand.Execute(null);
    }

    private async void OnOpenShowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShowEditorViewModel vm) return;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Show",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PyroPilot Show") { Patterns = [$"*{ShowPackage.FileExtension}"] }],
        });

        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;

        try { vm.Load(path); }
        catch (Exception ex) { vm.StatusMessage = $"Couldn't open show: {ex.Message}"; }
    }

    private void OnSaveShowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShowEditorViewModel vm) return;
        if (vm.FilePath is null)
        {
            OnSaveAsShowClick(sender, e);
            return;
        }

        try { vm.Save(); }
        catch (Exception ex) { vm.StatusMessage = $"Couldn't save show: {ex.Message}"; }
    }

    private async void OnSaveAsShowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShowEditorViewModel vm) return;
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Show As",
            SuggestedFileName = string.IsNullOrWhiteSpace(vm.ShowName) ? "Show" : vm.ShowName,
            DefaultExtension = ShowPackage.FileExtension.TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType("PyroPilot Show") { Patterns = [$"*{ShowPackage.FileExtension}"] }],
        });

        string? path = file?.TryGetLocalPath();
        if (path is null) return;
        if (!path.EndsWith(ShowPackage.FileExtension, StringComparison.OrdinalIgnoreCase)) path += ShowPackage.FileExtension;

        try { vm.Save(path); }
        catch (Exception ex) { vm.StatusMessage = $"Couldn't save show: {ex.Message}"; }
    }

    private async void OnImportAudioClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShowEditorViewModel vm) return;
        TrackViewModel? audioTrack = vm.Tracks.LastOrDefault(t => t.Kind == TrackKind.Audio);
        if (audioTrack is null)
        {
            vm.StatusMessage = "Add an Audio track first.";
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Audio",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Audio") { Patterns = ["*.mp3", "*.wav", "*.m4a", "*.flac", "*.aac"] }],
        });

        string? localPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (localPath is null)
        {
            vm.StatusMessage = "Only local files are supported for audio import.";
            return;
        }

        await vm.ImportAudioAsync(audioTrack, localPath);
    }

    private void OnRemoveTrackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShowEditorViewModel vm) return;
        if (sender is Button { Tag: TrackViewModel track }) vm.RemoveTrackCommand.Execute(track);
    }

    private void OnCueDeviceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ShowEditorViewModel vm) return;
        if (sender is ComboBox { SelectedItem: DeviceRowViewModel row }) vm.AssignDeviceToSelectedCue(row);
    }

    private void OnSeekSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is ShowEditorViewModel vm && sender is Slider slider) vm.Seek((int)slider.Value);
    }

    // --- Palette drag source ---

    private async void OnPaletteItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: FireworkDefinition firework } border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(FireworkDragFormat, firework));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
    }

    // --- Track drop target ---

    private void OnTrackDragEnter(object? sender, DragEventArgs e)
    {
        if (sender is Canvas canvas) canvas.Background = new SolidColorBrush(Color.Parse("#22FFFFFF"));
    }

    private void OnTrackDragOver(object? sender, DragEventArgs e)
    {
        bool canDrop = sender is Canvas { Tag: TrackViewModel { Kind: TrackKind.Fire } };
        e.DragEffects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnTrackDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Canvas canvas) canvas.Background = Brushes.Transparent;
    }

    private void OnTrackDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Canvas { Tag: TrackViewModel track } canvas) return;
        canvas.Background = Brushes.Transparent;
        if (DataContext is not ShowEditorViewModel vm) return;

        if (track.Kind != TrackKind.Fire)
        {
            vm.StatusMessage = "Fireworks can only be dropped on Fire tracks.";
            return;
        }

        FireworkDefinition? firework = e.DataTransfer.TryGetValue(FireworkDragFormat);
        if (firework is null) return;

        Point pos = e.GetPosition(canvas);
        int startMs = Math.Max(0, (int)(pos.X / vm.PixelsPerMs));
        vm.TryAddFireCue(track, firework, startMs);
    }

    // --- Clip select + move-drag ---

    private void OnClipPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: ClipViewModel clip } border) return;
        if (DataContext is not ShowEditorViewModel vm) return;

        _selectedClipBorder?.Classes.Remove("clipSelected");
        border.Classes.Add("clipSelected");
        _selectedClipBorder = border;
        vm.SelectedClip = clip;

        var found = FindTrackCanvas(border);
        if (found is null) return;

        (_dragCanvas, _dragTrack) = found.Value;
        _draggingClipBorder = border;
        _dragStartPointerPos = e.GetCurrentPoint(_dragCanvas).Position;
        _dragOriginalStartMs = clip.StartMs;
        e.Pointer.Capture(border);
    }

    private void OnClipPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingClipBorder is null || _dragCanvas is null || _dragTrack is null) return;
        if (sender is not Border { DataContext: ClipViewModel clip } border || !ReferenceEquals(border, _draggingClipBorder)) return;
        if (DataContext is not ShowEditorViewModel vm) return;

        Point pos = e.GetCurrentPoint(_dragCanvas).Position;
        double deltaPx = pos.X - _dragStartPointerPos.X;
        int deltaMs = (int)(deltaPx / vm.PixelsPerMs);
        vm.TryMoveClip(_dragTrack, clip, _dragOriginalStartMs + deltaMs);
    }

    private void OnClipPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border) e.Pointer.Capture(null);
        _draggingClipBorder = null;
        _dragCanvas = null;
        _dragTrack = null;
    }

    // --- Clip resize-drag ---

    private void OnResizeHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Border { DataContext: ClipViewModel clip } handle) return;

        var found = FindTrackCanvas(handle);
        if (found is null) return;

        (_resizeCanvas, _resizeTrack) = found.Value;
        _resizingHandle = handle;
        _resizingClip = clip;
        _resizeStartPointerX = e.GetCurrentPoint(_resizeCanvas).Position.X;
        _resizeOriginalDurationMs = clip.DurationMs;
        e.Pointer.Capture(handle);
    }

    private void OnResizeHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizingHandle is null || _resizingClip is null || _resizeCanvas is null || _resizeTrack is null) return;
        if (!ReferenceEquals(sender, _resizingHandle)) return;
        if (DataContext is not ShowEditorViewModel vm) return;

        double x = e.GetCurrentPoint(_resizeCanvas).Position.X;
        double deltaPx = x - _resizeStartPointerX;
        int deltaMs = (int)(deltaPx / vm.PixelsPerMs);
        vm.TryResizeClip(_resizeTrack, _resizingClip, _resizeOriginalDurationMs + deltaMs);
    }

    private void OnResizeHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border) e.Pointer.Capture(null);
        _resizingHandle = null;
        _resizingClip = null;
        _resizeCanvas = null;
        _resizeTrack = null;
    }

    private static (Canvas Canvas, TrackViewModel Track)? FindTrackCanvas(StyledElement element)
    {
        StyledElement? current = element.Parent;
        while (current is not null)
        {
            if (current is Canvas { Tag: TrackViewModel track } canvas) return (canvas, track);
            current = current.Parent;
        }
        return null;
    }
}
