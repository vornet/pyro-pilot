using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using PyroPilot.App.Services;
using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

/// <summary>
/// The video-editor-style show timeline: multiple tracks of clips, a
/// transport clock that drives both the preview animation and (optionally)
/// real device firing, and save/load through <see cref="ShowWorkspaceService"/>.
/// </summary>
public partial class ShowEditorViewModel : ViewModelBase
{
    private readonly ShowWorkspaceService _workspace;
    private readonly DeviceSessionRegistry _registry;
    private readonly AudioPlaybackService _audio;
    private readonly TimelineScale _scale = new();
    private readonly DispatcherTimer _timer;
    private readonly HashSet<Guid> _firedThisRun = [];
    private DateTime _lastTickUtc;
    private string? _loadedAudioFileName;
    private readonly Dictionary<Guid, Bitmap> _previewImages = [];

    public LibraryViewModel Library { get; }
    public DevicesViewModel Devices { get; }

    public ObservableCollection<TrackViewModel> Tracks { get; } = [];
    public ObservableCollection<PreviewBurstViewModel> Bursts { get; } = [];
    public Show PreviewShow => _workspace.Show;
    public Bitmap? ActivePreviewImage
    {
        get
        {
            FireCue? cue = _workspace.Show.Tracks
                .Where(track => track.Kind == TrackKind.Fire && !track.Muted)
                .SelectMany(track => track.Clips)
                .OfType<FireCue>()
                .LastOrDefault(item => CurrentTimeMs >= item.StartMs && CurrentTimeMs < item.EndMs);
            return cue is not null && _previewImages.TryGetValue(cue.FireworkDefinitionId, out Bitmap? image) ? image : null;
        }
    }
    public bool HasActivePreviewImage => ActivePreviewImage is not null;
    public string ActivePreviewLabel
    {
        get
        {
            FireCue? cue = _workspace.Show.Tracks.SelectMany(track => track.Clips).OfType<FireCue>()
                .LastOrDefault(item => CurrentTimeMs >= item.StartMs && CurrentTimeMs < item.EndMs);
            if (cue is null) return "No firework active";
            return _workspace.Show.Library.FirstOrDefault(item => item.Id == cue.FireworkDefinitionId)?.Name ?? cue.Label;
        }
    }

    [ObservableProperty]
    private string _showName = "New Show";

    [ObservableProperty]
    private double _zoomPxPerSecond = 60;

    [ObservableProperty]
    private int _currentTimeMs;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _liveFireEnabled;

    [ObservableProperty]
    private ClipViewModel? _selectedClip;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _filePath;

    public double PixelsPerMs => _scale.PixelsPerMs;

    public int TotalDurationMs => Math.Max(
        5000,
        Tracks.SelectMany(t => t.Clips).Select(c => c.EndMs).DefaultIfEmpty(0).Max());

    /// <summary>Scrollable width of the timeline content, with trailing headroom past the last clip.</summary>
    public double TimelineWidthPx => TotalDurationMs * PixelsPerMs + 200;

    /// <summary>Playhead position on the timeline canvas, in pixels.</summary>
    public double PlayheadX => CurrentTimeMs * PixelsPerMs;

    /// <summary>Whole-second tick marks for the ruler, spanning the current timeline width.</summary>
    public IReadOnlyList<int> RulerSeconds => Enumerable.Range(0, TotalDurationMs / 1000 + 2).ToList();

    public FireCueViewModel? SelectedFireCue => SelectedClip as FireCueViewModel;
    public AudioClipViewModel? SelectedAudioClip => SelectedClip as AudioClipViewModel;
    public DeviceRowViewModel? SelectedCueDevice
    {
        get => SelectedFireCue?.DeviceId is { } id
            ? Devices.Devices.FirstOrDefault(device => device.Model.Id == id)
            : null;
        set => AssignDeviceToSelectedCue(value);
    }

    public ShowEditorViewModel(
        ShowWorkspaceService workspace,
        DeviceSessionRegistry registry,
        AudioPlaybackService audio,
        LibraryViewModel library,
        DevicesViewModel devices)
    {
        _workspace = workspace;
        _registry = registry;
        _audio = audio;
        Library = library;
        Devices = devices;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += OnTick;

        workspace.ShowReplaced += RebuildFromWorkspace;
        RebuildFromWorkspace();
    }

    private void RebuildFromWorkspace()
    {
        StopPlayback();
        RepairMissingLibrarySnapshots();
        Tracks.Clear();
        foreach (Track t in _workspace.Show.Tracks) Tracks.Add(new TrackViewModel(t, _scale));
        ShowName = _workspace.Show.Name;
        FilePath = _workspace.FilePath;
        _loadedAudioFileName = null;
        foreach (Bitmap image in _previewImages.Values) image.Dispose();
        _previewImages.Clear();
        foreach (FireworkDefinition definition in _workspace.Show.Library.Where(item => item.PreviewImageData is not null))
        {
            try { _previewImages[definition.Id] = new Bitmap(new MemoryStream(definition.PreviewImageData!)); }
            catch { /* A corrupt optional image must not prevent opening a show. */ }
        }
        OnPropertyChanged(nameof(PreviewShow));
        NotifyMediaPreviewChanged();
        NotifyDurationChanged();
    }

    partial void OnShowNameChanged(string value) => _workspace.Show.Name = value;

    partial void OnSelectedClipChanged(ClipViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedFireCue));
        OnPropertyChanged(nameof(SelectedAudioClip));
        OnPropertyChanged(nameof(SelectedCueDevice));
    }

    partial void OnZoomPxPerSecondChanged(double value)
    {
        _scale.PixelsPerMs = value / 1000.0;
        OnPropertyChanged(nameof(PixelsPerMs));
        OnPropertyChanged(nameof(TimelineWidthPx));
        OnPropertyChanged(nameof(PlayheadX));
    }

    partial void OnCurrentTimeMsChanged(int value)
    {
        OnPropertyChanged(nameof(PlayheadX));
        NotifyMediaPreviewChanged();
    }

    private void NotifyMediaPreviewChanged()
    {
        OnPropertyChanged(nameof(ActivePreviewImage));
        OnPropertyChanged(nameof(HasActivePreviewImage));
        OnPropertyChanged(nameof(ActivePreviewLabel));
    }

    private void NotifyDurationChanged()
    {
        OnPropertyChanged(nameof(TotalDurationMs));
        OnPropertyChanged(nameof(TimelineWidthPx));
        OnPropertyChanged(nameof(RulerSeconds));
    }

    [RelayCommand]
    private void AddFireTrack() => AddTrack(TrackKind.Fire, $"Fire {Tracks.Count(t => t.Kind == TrackKind.Fire) + 1}");

    [RelayCommand]
    private void AddAudioTrack() => AddTrack(TrackKind.Audio, "Music");

    private void AddTrack(TrackKind kind, string name)
    {
        var track = new Track { Name = name, Kind = kind };
        _workspace.Show.Tracks.Add(track);
        Tracks.Add(new TrackViewModel(track, _scale));
        _workspace.MarkDirty();
    }

    [RelayCommand]
    private void RemoveTrack(TrackViewModel track)
    {
        _workspace.Show.Tracks.Remove(track.Model);
        Tracks.Remove(track);
        if (SelectedClip is not null && track.Clips.Contains(SelectedClip)) SelectedClip = null;
        _workspace.MarkDirty();
        NotifyDurationChanged();
    }

    public bool TryAddFireCue(TrackViewModel track, FireworkDefinition firework, int startMs)
    {
        var probe = new FireCue { StartMs = Math.Max(0, startMs), DurationMs = Math.Max(100, firework.DurationMs) };
        if (track.HasOverlap(probe))
        {
            StatusMessage = "Can't drop there -- it overlaps another cue on this track.";
            return false;
        }

        EnsureLibrarySnapshot(firework);
        FireCueViewModel vm = track.AddFireCue(firework, startMs, deviceId: null, port: 1);
        SelectedClip = vm;
        _workspace.MarkDirty();
        NotifyDurationChanged();
        return true;
    }

    public bool TryMoveClip(TrackViewModel track, ClipViewModel clip, int newStartMs)
    {
        int originalStart = clip.StartMs;
        clip.StartMs = Math.Max(0, newStartMs);
        if (track.HasOverlap(clip.Model))
        {
            clip.StartMs = originalStart;
            return false;
        }

        _workspace.MarkDirty();
        NotifyDurationChanged();
        return true;
    }

    public bool TryResizeClip(TrackViewModel track, ClipViewModel clip, int newDurationMs)
    {
        int original = clip.DurationMs;
        clip.DurationMs = newDurationMs;
        if (track.HasOverlap(clip.Model))
        {
            clip.DurationMs = original;
            return false;
        }

        _workspace.MarkDirty();
        NotifyDurationChanged();
        return true;
    }

    [RelayCommand]
    private void RemoveSelectedClip()
    {
        if (SelectedClip is null) return;
        TrackViewModel? track = Tracks.FirstOrDefault(t => t.Clips.Contains(SelectedClip));
        track?.RemoveClip(SelectedClip);
        SelectedClip = null;
        _workspace.MarkDirty();
        NotifyDurationChanged();
    }

    public void AssignDeviceToSelectedCue(DeviceRowViewModel? row)
    {
        if (SelectedFireCue is null) return;
        SelectedFireCue.DeviceId = row?.Model.Id;
        OnPropertyChanged(nameof(SelectedCueDevice));
        _workspace.MarkDirty();
    }

    public async Task ImportAudioAsync(TrackViewModel track, string sourcePath)
    {
        string fileName = _workspace.ImportAudio(sourcePath);
        int durationMs;
        using (var reader = new AudioFileReader(sourcePath))
            durationMs = (int)reader.TotalTime.TotalMilliseconds;

        track.AddAudioClip(fileName, 0, Math.Max(1000, durationMs));
        NotifyDurationChanged();
        await Task.CompletedTask;
    }

    // --- Transport ---

    [RelayCommand]
    private void Play()
    {
        if (IsPlaying) return;
        if (LiveFireEnabled && !ValidateLiveFireCues()) return;

        EnsureAudioLoadedForPlayback();
        _audio.PlayFrom(TimeSpan.FromMilliseconds(CurrentTimeMs));
        _lastTickUtc = DateTime.UtcNow;
        _timer.Start();
        IsPlaying = true;
    }

    private bool ValidateLiveFireCues()
    {
        IEnumerable<FireCueViewModel> upcomingCues = Tracks
            .Where(track => track.Kind == TrackKind.Fire && !track.Muted)
            .SelectMany(track => track.Clips.OfType<FireCueViewModel>())
            .Where(cue => cue.StartMs >= CurrentTimeMs);

        foreach (FireCueViewModel cue in upcomingCues)
        {
            if (cue.DeviceId is not { } deviceId)
            {
                SelectedClip = cue;
                StatusMessage = $"LIVE FIRE NOT STARTED: Assign a device to '{cue.Label}'.";
                return false;
            }

            if (!_registry.TryGet(deviceId, out _))
            {
                SelectedClip = cue;
                StatusMessage = $"LIVE FIRE NOT STARTED: The device for '{cue.Label}' is not connected.";
                return false;
            }
        }

        StatusMessage = null;
        return true;
    }

    private void RepairMissingLibrarySnapshots()
    {
        HashSet<Guid> referencedIds = _workspace.Show.Tracks
            .SelectMany(track => track.Clips)
            .OfType<FireCue>()
            .Select(cue => cue.FireworkDefinitionId)
            .ToHashSet();

        foreach (Guid id in referencedIds)
        {
            if (_workspace.Show.Library.Any(item => item.Id == id)) continue;
            FireworkDefinition? source = Library.Fireworks.FirstOrDefault(item => item.Id == id);
            if (source is not null) EnsureLibrarySnapshot(source);
        }
    }

    private void EnsureLibrarySnapshot(FireworkDefinition source)
    {
        if (_workspace.Show.Library.Any(item => item.Id == source.Id)) return;

        FireworkEffect effect = source.Effect ?? new FireworkEffect();
        _workspace.Show.Library.Add(new FireworkDefinition
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Category = source.Category,
            DurationMs = source.DurationMs,
            ColorHex = source.ColorHex,
            PreviewImageData = source.PreviewImageData?.ToArray(),
            PreviewImageFileName = source.PreviewImageFileName,
            VideoUrl = source.VideoUrl,
            Effect = new FireworkEffect
            {
                Shape = effect.Shape,
                BurstTimeSeconds = effect.BurstTimeSeconds,
                LaunchSpeed = effect.LaunchSpeed,
                BurstSpeed = effect.BurstSpeed,
                ParticleLifetimeSeconds = effect.ParticleLifetimeSeconds,
                ParticleCount = effect.ParticleCount,
                Gravity = effect.Gravity,
                Drag = effect.Drag,
                Colors = [.. effect.Colors],
                Layers = effect.Layers.Select(layer => new ParticleEffectLayer
                {
                    Name = layer.Name,
                    DelaySeconds = layer.DelaySeconds,
                    Shape = layer.Shape,
                    Speed = layer.Speed,
                    LifetimeSeconds = layer.LifetimeSeconds,
                    ParticleCount = layer.ParticleCount,
                    Gravity = layer.Gravity,
                    Drag = layer.Drag,
                    TrailSamples = layer.TrailSamples,
                    TrailSpacingSeconds = layer.TrailSpacingSeconds,
                    Twinkle = layer.Twinkle,
                    SparkSize = layer.SparkSize,
                    TrailSize = layer.TrailSize,
                    Colors = [.. layer.Colors],
                }).ToList(),
            },
        });
    }

    [RelayCommand]
    private void Pause()
    {
        _timer.Stop();
        _audio.Pause();
        IsPlaying = false;
    }

    [RelayCommand]
    private void Stop() => StopPlayback();

    private void StopPlayback()
    {
        _timer.Stop();
        _audio.Stop();
        IsPlaying = false;
        CurrentTimeMs = 0;
        Bursts.Clear();
        _firedThisRun.Clear();
    }

    public void Seek(int newTimeMs)
    {
        CurrentTimeMs = Math.Clamp(newTimeMs, 0, TotalDurationMs);
        _audio.Seek(TimeSpan.FromMilliseconds(CurrentTimeMs));
        // Re-arm live-fire for anything at/after the new position so scrubbing back doesn't skip cues.
        _firedThisRun.RemoveWhere(id => Tracks.SelectMany(t => t.Clips).Any(c => c.Id == id && c.StartMs >= CurrentTimeMs));
    }

    private void EnsureAudioLoadedForPlayback()
    {
        AudioClipViewModel? audioClip = Tracks
            .Where(t => t.Kind == TrackKind.Audio && !t.Muted)
            .SelectMany(t => t.Clips)
            .OfType<AudioClipViewModel>()
            .FirstOrDefault();

        if (audioClip is null)
        {
            _audio.Unload();
            _loadedAudioFileName = null;
            return;
        }

        if (_loadedAudioFileName == audioClip.Clip.FileName) return;

        try
        {
            string path = _workspace.ResolveAudioPlaybackPath(audioClip.Clip.FileName);
            _audio.Load(path);
            _loadedAudioFileName = audioClip.Clip.FileName;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load audio: {ex.Message}";
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        int deltaMs = (int)(now - _lastTickUtc).TotalMilliseconds;
        _lastTickUtc = now;

        int previousTimeMs = CurrentTimeMs;
        CurrentTimeMs += deltaMs;

        FireDueCues(previousTimeMs, CurrentTimeMs);
        TickBursts();

        if (CurrentTimeMs >= TotalDurationMs) StopPlayback();
    }

    private void FireDueCues(int fromMs, int toMs)
    {
        foreach (TrackViewModel track in Tracks)
        {
            if (track.Kind != TrackKind.Fire || track.Muted) continue;

            foreach (FireCueViewModel cue in track.Clips.OfType<FireCueViewModel>())
            {
                if (cue.StartMs < fromMs || cue.StartMs >= toMs) continue;
                if (!_firedThisRun.Add(cue.Id)) continue;

                SpawnBurst(track, cue);

                if (LiveFireEnabled && cue.DeviceId is { } deviceId && _registry.TryGet(deviceId, out IDeviceSession session))
                    _ = FireLiveAsync(session, cue);
            }
        }
    }

    private async Task FireLiveAsync(IDeviceSession session, FireCueViewModel cue)
    {
        try
        {
            bool ok = await session.ManualFireAsync(cue.Port);
            StatusMessage = ok
                ? $"LIVE FIRE SENT: {cue.Label} (port {cue.Port})"
                : $"LIVE FIRE FAILED: {cue.Label} (port {cue.Port})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"LIVE FIRE ERROR: {cue.Label} -- {ex.Message}";
        }
    }

    private void SpawnBurst(TrackViewModel track, FireCueViewModel cue)
    {
        List<TrackViewModel> fireTracks = Tracks.Where(t => t.Kind == TrackKind.Fire).ToList();
        int fireIndex = Math.Max(0, fireTracks.IndexOf(track));
        double laneX = fireTracks.Count <= 1 ? 0.5 : (fireIndex + 0.5) / fireTracks.Count;
        Bursts.Add(new PreviewBurstViewModel { LaneX = laneX, ColorHex = cue.ColorHex });
    }

    private void TickBursts()
    {
        for (int i = Bursts.Count - 1; i >= 0; i--)
        {
            if (Bursts[i].Tick()) Bursts.RemoveAt(i);
        }
    }

    // --- Save / load ---

    [RelayCommand]
    private void NewShow() => _workspace.New();

    public void Load(string path) => _workspace.Load(path);

    public void Save(string? path = null)
    {
        _workspace.Save(path);
        FilePath = _workspace.FilePath;
        StatusMessage = $"Saved to {FilePath}";
    }
}
