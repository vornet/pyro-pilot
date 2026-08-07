using System.Text.Json;
using PyroPilot.Core.Model;

namespace PyroPilot.Core.Persistence;

/// <summary>Reads and validates approved community-catalog snapshots.</summary>
public static class CommunityCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static CommunityCatalog Load(Stream stream)
    {
        CommunityCatalog catalog = JsonSerializer.Deserialize<CommunityCatalog>(stream, JsonOptions)
            ?? throw new InvalidDataException("The community catalog is empty.");
        Validate(catalog);
        return catalog;
    }

    public static void Save(Stream stream, CommunityCatalog catalog)
    {
        Validate(catalog);
        JsonSerializer.Serialize(stream, catalog, JsonOptions);
    }

    public static FireworkDefinition Import(FireworkDefinition approved, int catalogRevision)
    {
        ArgumentNullException.ThrowIfNull(approved);
        if (catalogRevision < 1) throw new ArgumentOutOfRangeException(nameof(catalogRevision));

        FireworkEffect effect = approved.Effect ?? new FireworkEffect();
        return new FireworkDefinition
        {
            Id = approved.Id,
            CommunityRevision = catalogRevision,
            Manufacturer = approved.Manufacturer,
            ProductCode = approved.ProductCode,
            Upc = approved.Upc,
            SourceUrl = approved.SourceUrl,
            Name = approved.Name,
            Description = approved.Description,
            Category = approved.Category,
            DurationMs = approved.DurationMs,
            ColorHex = approved.ColorHex,
            Effect = new FireworkEffect
            {
                Shape = effect.Shape,
                BurstTimeSeconds = effect.BurstTimeSeconds,
                LaunchSpeed = effect.LaunchSpeed,
                BurstSpeed = effect.BurstSpeed,
                ParticleLifetimeSeconds = effect.ParticleLifetimeSeconds,
                ParticleCount = effect.ParticleCount,
                Gravity = effect.Gravity,
                Drag = effect.Drag,
                Colors = [.. effect.Colors],
                Layers = effect.Layers.Select(CloneLayer).ToList(),
            },
        };
    }

    private static ParticleEffectLayer CloneLayer(ParticleEffectLayer layer) => new()
    {
        Name = layer.Name,
        DelaySeconds = layer.DelaySeconds,
        Shape = layer.Shape,
        Speed = layer.Speed,
        LifetimeSeconds = layer.LifetimeSeconds,
        ParticleCount = layer.ParticleCount,
        Gravity = layer.Gravity,
        Drag = layer.Drag,
        TrailSamples = layer.TrailSamples,
        TrailSpacingSeconds = layer.TrailSpacingSeconds,
        Twinkle = layer.Twinkle,
        Colors = [.. layer.Colors],
    };

    private static void Validate(CommunityCatalog catalog)
    {
        if (catalog.SchemaVersion != CommunityCatalog.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported community catalog schema {catalog.SchemaVersion}.");
        if (catalog.Revision < 1) throw new InvalidDataException("Catalog revision must be positive.");
        if (catalog.GeneratedAtUtc == default) throw new InvalidDataException("Catalog generation time is missing.");

        var ids = new HashSet<Guid>();
        foreach (FireworkDefinition firework in catalog.Fireworks)
        {
            if (firework.Id == Guid.Empty || !ids.Add(firework.Id))
                throw new InvalidDataException("Every community firework must have a unique non-empty ID.");
            if (string.IsNullOrWhiteSpace(firework.Name))
                throw new InvalidDataException("Every community firework must have a name.");
            if (firework.DurationMs < 100 || firework.DurationMs > 900_000)
                throw new InvalidDataException($"{firework.Name} has an invalid duration.");
            if (firework.Effect is null)
                throw new InvalidDataException($"{firework.Name} has no effect definition.");
        }
    }
}
