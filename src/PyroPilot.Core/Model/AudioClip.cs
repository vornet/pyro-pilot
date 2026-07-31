namespace PyroPilot.Core.Model;

/// <summary>A segment of an audio file placed on an Audio track.</summary>
public sealed class AudioClip : TimelineClip
{
    /// <summary>File name within the show package's audio/ folder (see ShowPackage).</summary>
    public string FileName { get; set; } = "";

    public double Volume { get; set; } = 1.0;

    /// <summary>Trim-in point within the source audio file, in milliseconds.</summary>
    public int SourceOffsetMs { get; set; }
}
