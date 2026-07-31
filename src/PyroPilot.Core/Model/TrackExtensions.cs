namespace PyroPilot.Core.Model;

public static class TrackExtensions
{
    /// <summary>True if <paramref name="candidate"/> would overlap any other clip already on this track.</summary>
    public static bool HasOverlap(this Track track, TimelineClip candidate)
    {
        foreach (TimelineClip clip in track.Clips)
        {
            if (clip.Id == candidate.Id) continue;
            if (candidate.StartMs < clip.EndMs && clip.StartMs < candidate.EndMs) return true;
        }
        return false;
    }
}
