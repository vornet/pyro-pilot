namespace PyroPilot.Core.Model;

/// <summary>Whether a <see cref="Track"/> holds <see cref="FireCue"/> or <see cref="AudioClip"/> clips.</summary>
public enum TrackKind
{
    Fire,
    Audio,
}

/// <summary>
/// One lane on the show timeline. Clips within a single track may not overlap
/// (like a video editor track); overlapping effects are achieved by placing
/// them on separate tracks instead -- see <see cref="TrackExtensions.HasOverlap"/>.
/// </summary>
public sealed class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Track";
    public TrackKind Kind { get; set; } = TrackKind.Fire;
    public bool Muted { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public List<TimelineClip> Clips { get; set; } = [];
}
