using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

/// <summary>
/// One lane in the timeline editor. Clips within a track may not overlap
/// (like a video-editor track); overlap is how <see cref="TrackExtensions.HasOverlap"/>
/// is used by <see cref="ShowEditorViewModel"/> before accepting a drop, move, or resize.
/// </summary>
public partial class TrackViewModel : ViewModelBase
{
    private readonly TimelineScale _scale;

    public Track Model { get; }
    public ObservableCollection<ClipViewModel> Clips { get; }
    public TrackKind Kind => Model.Kind;
    public string ColorHex => Model.ColorHex;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _muted;

    public TrackViewModel(Track model, TimelineScale scale)
    {
        Model = model;
        _scale = scale;
        _name = model.Name;
        _muted = model.Muted;
        Clips = new ObservableCollection<ClipViewModel>(model.Clips.Select(WrapClip));
    }

    partial void OnNameChanged(string value) => Model.Name = value;
    partial void OnMutedChanged(bool value) => Model.Muted = value;

    private ClipViewModel WrapClip(TimelineClip clip) => clip switch
    {
        FireCue fc => new FireCueViewModel(fc, _scale),
        AudioClip ac => new AudioClipViewModel(ac, _scale),
        _ => throw new NotSupportedException($"Unknown clip type '{clip.GetType()}'."),
    };

    public bool HasOverlap(TimelineClip candidate) => Model.HasOverlap(candidate);

    public FireCueViewModel AddFireCue(FireworkDefinition firework, int startMs, Guid? deviceId, int port)
    {
        var cue = new FireCue
        {
            FireworkDefinitionId = firework.Id,
            Label = firework.Name,
            ColorHex = firework.ColorHex,
            StartMs = Math.Max(0, startMs),
            DurationMs = Math.Max(100, firework.DurationMs),
            DeviceId = deviceId,
            Port = port,
        };
        Model.Clips.Add(cue);
        var vm = new FireCueViewModel(cue, _scale);
        Clips.Add(vm);
        return vm;
    }

    public AudioClipViewModel AddAudioClip(string fileName, int startMs, int durationMs)
    {
        var clip = new AudioClip { FileName = fileName, StartMs = Math.Max(0, startMs), DurationMs = Math.Max(100, durationMs) };
        Model.Clips.Add(clip);
        var vm = new AudioClipViewModel(clip, _scale);
        Clips.Add(vm);
        return vm;
    }

    public void RemoveClip(ClipViewModel clip)
    {
        Model.Clips.Remove(clip.Model);
        Clips.Remove(clip);
    }
}
