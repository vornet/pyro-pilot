using System.Text.Json.Serialization;

namespace PyroPilot.Core.Model;

/// <summary>
/// One item placed on a <see cref="Track"/>'s timeline, positioned like a clip
/// in a video editor: a start time and a duration. Concrete types are
/// <see cref="FireCue"/> (a firework shot) and <see cref="AudioClip"/> (a
/// music/audio segment).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(FireCue), "fireCue")]
[JsonDerivedType(typeof(AudioClip), "audioClip")]
public abstract class TimelineClip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int StartMs { get; set; }
    public int DurationMs { get; set; } = 1000;

    [JsonIgnore]
    public int EndMs => StartMs + DurationMs;
}
