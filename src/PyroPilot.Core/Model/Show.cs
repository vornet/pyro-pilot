namespace PyroPilot.Core.Model;

/// <summary>
/// A complete firework show: its timeline tracks, the paired devices it fires
/// to, and a self-contained snapshot of the firework definitions its cues
/// reference (so a saved show stays meaningful even if the operator's global
/// library later changes -- see PyroPilot.Core.Persistence.ShowPackage).
/// </summary>
public sealed class Show
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Show";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<Track> Tracks { get; set; } = [];
    public List<FireworkDefinition> Library { get; set; } = [];
    public List<PairedDevice> Devices { get; set; } = [];

    /// <summary>End time of the last clip on any track, in milliseconds.</summary>
    public int ComputeDurationMs() =>
        Tracks.SelectMany(t => t.Clips).Select(c => c.EndMs).DefaultIfEmpty(0).Max();
}
