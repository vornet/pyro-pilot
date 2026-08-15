using PyroPilot.Core.Model;
using PyroPilot.Core.Simulation;

namespace PyroPilot.Core.Tests;

public class FireworkSimulatorTests
{
    [Fact]
    public void Sample_IsDeterministicAtAnAbsoluteTime()
    {
        var effect = new FireworkEffect { ParticleCount = 32, BurstTimeSeconds = 1f };
        var cue = new FireCue { SimulationSeed = 8675309 };

        IReadOnlyList<ParticleSnapshot> first = FireworkSimulator.Sample(effect, cue, 1.5f);
        IReadOnlyList<ParticleSnapshot> second = FireworkSimulator.Sample(effect, cue, 1.5f);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Sample_ReturnsACometBeforeBurst()
    {
        var effect = new FireworkEffect { BurstTimeSeconds = 2f };

        IReadOnlyList<ParticleSnapshot> particles = FireworkSimulator.Sample(effect, new FireCue(), 1f);

        Assert.NotEmpty(particles);
        Assert.All(particles, particle => Assert.Equal(ParticleKind.Comet, particle.Kind));
        Assert.True(particles[^1].Position.Y > 0);
    }

    [Fact]
    public void Sample_ReturnsConfiguredSparkCountAfterBurst()
    {
        var effect = new FireworkEffect { BurstTimeSeconds = 1f, ParticleCount = 48 };

        IReadOnlyList<ParticleSnapshot> particles = FireworkSimulator.Sample(effect, new FireCue(), 1.1f);

        float headSize = particles.Max(particle => particle.Size);
        Assert.Equal(48, particles.Count(particle => particle.Size == headSize));
        Assert.True(particles.Count > 48); // trail samples accompany the live spark heads
        Assert.All(particles, particle => Assert.Equal(ParticleKind.Spark, particle.Kind));
    }

    [Fact]
    public void Sample_ReturnsNothingAfterEffectLifetime()
    {
        var effect = new FireworkEffect { BurstTimeSeconds = 1f, ParticleLifetimeSeconds = 2f };

        Assert.Empty(FireworkSimulator.Sample(effect, new FireCue(), 3f));
    }

    [Fact]
    public void RingPresetKeepsBurstParticlesInACameraFacingPlane()
    {
        var effect = new FireworkEffect { Shape = BurstShape.Ring, BurstTimeSeconds = 1f, ParticleCount = 12 };

        IReadOnlyList<ParticleSnapshot> atBurst = FireworkSimulator.Sample(effect, new FireCue(), 1.1f);

        float depthSpan = atBurst.Max(particle => particle.Position.Z) - atBurst.Min(particle => particle.Position.Z);
        float widthSpan = atBurst.Max(particle => particle.Position.X) - atBurst.Min(particle => particle.Position.X);
        float heightSpan = atBurst.Max(particle => particle.Position.Y) - atBurst.Min(particle => particle.Position.Y);
        Assert.True(depthSpan < widthSpan * 0.15f);
        Assert.True(depthSpan < heightSpan * 0.15f);
    }

    [Fact]
    public void DelayedLayerAppearsOnlyAfterItsDelay()
    {
        var effect = new FireworkEffect
        {
            BurstTimeSeconds = 1f,
            Layers = [new ParticleEffectLayer { DelaySeconds = 0.5f, ParticleCount = 10, TrailSamples = 0 }],
        };

        Assert.Empty(FireworkSimulator.Sample(effect, new FireCue(), 1.4f));
        Assert.Equal(10, FireworkSimulator.Sample(effect, new FireCue(), 1.6f).Count);
    }

    [Fact]
    public void LayerTrailProducesFadingHistorySamples()
    {
        var effect = new FireworkEffect
        {
            BurstTimeSeconds = 1f,
            Layers = [new ParticleEffectLayer { ParticleCount = 1, TrailSamples = 4, TrailSpacingSeconds = 0.05f }],
        };

        IReadOnlyList<ParticleSnapshot> particles = FireworkSimulator.Sample(effect, new FireCue(), 1.3f);

        Assert.Equal(5, particles.Count);
        Assert.True(particles[0].Brightness < particles[^1].Brightness);
    }

    [Fact]
    public void LayerUsesIndependentHeadAndTrailSizes()
    {
        var effect = new FireworkEffect
        {
            BurstTimeSeconds = 1f,
            Layers = [new ParticleEffectLayer
            {
                ParticleCount = 1,
                TrailSamples = 2,
                TrailSpacingSeconds = 0.05f,
                SparkSize = 0.2f,
                TrailSize = 0.03f,
            }],
        };

        IReadOnlyList<ParticleSnapshot> particles = FireworkSimulator.Sample(effect, new FireCue(), 1.2f);

        Assert.Equal([0.03f, 0.03f, 0.2f], particles.Select(particle => particle.Size));
    }

    [Fact]
    public void BurstCrownExpandsOutwardOverItsLifetime()
    {
        var effect = new FireworkEffect
        {
            BurstTimeSeconds = 1f,
            Layers = [new ParticleEffectLayer
            {
                ParticleCount = 96,
                TrailSamples = 0,
                LifetimeSeconds = 2.4f,
            }],
        };
        var cue = new FireCue { SimulationSeed = 42 };

        IReadOnlyList<ParticleSnapshot> early = FireworkSimulator.Sample(effect, cue, 1.3f);
        IReadOnlyList<ParticleSnapshot> late = FireworkSimulator.Sample(effect, cue, 2.5f);

        Assert.True(AverageRadius(late) > AverageRadius(early) * 2f);
    }

    private static float AverageRadius(IReadOnlyList<ParticleSnapshot> particles)
    {
        System.Numerics.Vector3 center = particles.Aggregate(
            System.Numerics.Vector3.Zero,
            (sum, particle) => sum + particle.Position) / particles.Count;
        return particles.Average(particle => System.Numerics.Vector3.Distance(center, particle.Position));
    }
}
