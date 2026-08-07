using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.Core.Tests;

public class CommunityCatalogStoreTests
{
    [Fact]
    public void RoundTripsApprovedCatalog()
    {
        var catalog = new CommunityCatalog
        {
            Revision = 7,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Fireworks = [new FireworkDefinition
            {
                Manufacturer = "Example Fireworks",
                ProductCode = "EX-100",
                Name = "Golden Example",
                DurationMs = 30_000,
            }],
        };

        using var stream = new MemoryStream();
        CommunityCatalogStore.Save(stream, catalog);
        stream.Position = 0;
        CommunityCatalog loaded = CommunityCatalogStore.Load(stream);

        Assert.Equal(7, loaded.Revision);
        Assert.Equal("Example Fireworks", loaded.Fireworks[0].Manufacturer);
        Assert.Equal("EX-100", loaded.Fireworks[0].ProductCode);
    }

    [Fact]
    public void RejectsDuplicateIds()
    {
        Guid duplicate = Guid.NewGuid();
        var catalog = new CommunityCatalog
        {
            Revision = 1,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Fireworks =
            [
                new FireworkDefinition { Id = duplicate, Name = "One" },
                new FireworkDefinition { Id = duplicate, Name = "Two" },
            ],
        };

        using var stream = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => CommunityCatalogStore.Save(stream, catalog));
    }

    [Fact]
    public void ImportClonesEffectAndRecordsRevision()
    {
        var approved = new FireworkDefinition
        {
            Name = "Community Cake",
            Effect = new FireworkEffect
            {
                Colors = ["#FF0000"],
                Layers = [new ParticleEffectLayer { Name = "First", Colors = ["#00FF00"] }],
            },
        };

        FireworkDefinition imported = CommunityCatalogStore.Import(approved, 4);
        approved.Effect.Colors[0] = "#000000";
        approved.Effect.Layers[0].Name = "Changed";

        Assert.Equal(approved.Id, imported.Id);
        Assert.Equal(4, imported.CommunityRevision);
        Assert.Equal("#FF0000", imported.Effect.Colors[0]);
        Assert.Equal("First", imported.Effect.Layers[0].Name);
    }
}
