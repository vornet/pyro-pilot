namespace PyroPilot.Core.Model;

/// <summary>A single firework shot placed on a Fire track, pointing at a library entry and a device port.</summary>
public sealed class FireCue : TimelineClip
{
    public Guid FireworkDefinitionId { get; set; }
    public string Label { get; set; } = "";
    public Guid? DeviceId { get; set; }
    public int Port { get; set; } = 1;
    public string ColorHex { get; set; } = "#FF7A00";

    /// <summary>Launch position in preview-world metres.</summary>
    public SpatialPoint LaunchPosition { get; set; } = new();

    /// <summary>Horizontal launch heading in degrees, where zero points downrange.</summary>
    public float HeadingDegrees { get; set; }

    /// <summary>Angle away from vertical in degrees.</summary>
    public float TiltDegrees { get; set; }

    /// <summary>Stable random seed used to make seeking and replay visually identical.</summary>
    public int SimulationSeed { get; set; }
}
