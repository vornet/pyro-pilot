namespace PyroPilot.Core.Model;

/// <summary>
/// A reusable "product" in the operator's firework library -- e.g. a specific
/// cake, shell, or fountain -- with the burn duration used to size its default
/// clip length on the timeline.
/// </summary>
public sealed class FireworkDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Revision of the approved community entry this definition came from, if any.</summary>
    public int? CommunityRevision { get; set; }
    public string? Manufacturer { get; set; }
    public string? ProductCode { get; set; }
    public string? Upc { get; set; }
    public string? SourceUrl { get; set; }
    /// <summary>Optional product/effect image embedded as bytes so library and show snapshots remain portable.</summary>
    public byte[]? PreviewImageData { get; set; }
    public string? PreviewImageFileName { get; set; }
    /// <summary>Optional YouTube or other web video showing the real product.</summary>
    public string? VideoUrl { get; set; }
    public string Name { get; set; } = "New Firework";
    public string? Description { get; set; }
    public string Category { get; set; } = "Uncategorized";

    /// <summary>How long the effect burns/lasts, in milliseconds. Drives a new clip's default duration.</summary>
    public int DurationMs { get; set; } = 3000;

    /// <summary>Hex color (e.g. "#FF7A00") used for this firework's clips on the timeline.</summary>
    public string ColorHex { get; set; } = "#FF7A00";

    /// <summary>
    /// Parameters used by the visual preview. Kept with the library definition so a
    /// product can have a reusable look without coupling the show model to a renderer.
    /// </summary>
    public FireworkEffect Effect { get; set; } = new();
}
