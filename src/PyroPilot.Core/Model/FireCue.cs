namespace PyroPilot.Core.Model;

/// <summary>A single firework shot placed on a Fire track, pointing at a library entry and a device port.</summary>
public sealed class FireCue : TimelineClip
{
    public Guid FireworkDefinitionId { get; set; }
    public string Label { get; set; } = "";
    public Guid? DeviceId { get; set; }
    public int Port { get; set; } = 1;
    public string ColorHex { get; set; } = "#FF7A00";
}
