namespace PyroPilot.Core.Model;

/// <summary>A versioned, approved snapshot downloaded from the community catalog.</summary>
public sealed class CommunityCatalog
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int Revision { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public List<FireworkDefinition> Fireworks { get; set; } = [];
}
