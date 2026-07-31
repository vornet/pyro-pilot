using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

public sealed class FireCueViewModel(FireCue cue, TimelineScale scale) : ClipViewModel(scale)
{
    public FireCue Cue { get; } = cue;
    public override TimelineClip Model => Cue;
    public override string Label => Cue.Label;
    public override string ColorHex => Cue.ColorHex;

    public string LabelText
    {
        get => Cue.Label;
        set
        {
            if (Cue.Label == value) return;
            Cue.Label = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Label));
        }
    }

    public int Port
    {
        get => Cue.Port;
        set
        {
            if (Cue.Port == value) return;
            Cue.Port = value;
            OnPropertyChanged();
        }
    }

    public Guid? DeviceId
    {
        get => Cue.DeviceId;
        set
        {
            if (Cue.DeviceId == value) return;
            Cue.DeviceId = value;
            OnPropertyChanged();
        }
    }
}
