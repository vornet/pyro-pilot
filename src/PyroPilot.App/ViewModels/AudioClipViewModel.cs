using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

public sealed class AudioClipViewModel(AudioClip clip, TimelineScale scale) : ClipViewModel(scale)
{
    public AudioClip Clip { get; } = clip;
    public override TimelineClip Model => Clip;
    public override string Label => Clip.FileName;
    public override string ColorHex => "#22C55E";
}
