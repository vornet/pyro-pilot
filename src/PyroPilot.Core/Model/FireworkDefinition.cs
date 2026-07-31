namespace PyroPilot.Core.Model;

/// <summary>
/// A reusable "product" in the operator's firework library -- e.g. a specific
/// cake, shell, or fountain -- with the burn duration used to size its default
/// clip length on the timeline.
/// </summary>
public sealed class FireworkDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Firework";
    public string? Description { get; set; }
    public string Category { get; set; } = "Uncategorized";

    /// <summary>How long the effect burns/lasts, in milliseconds. Drives a new clip's default duration.</summary>
    public int DurationMs { get; set; } = 3000;

    /// <summary>Hex color (e.g. "#FF7A00") used for this firework's clips on the timeline.</summary>
    public string ColorHex { get; set; } = "#FF7A00";
}
