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

        Assert.Equal(48, particles.Count(particle => particle.Size == 0.12f));
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
    public void RingPresetKeepsBurstParticlesInAHorizontalPlane()
    {
        var effect = new FireworkEffect { Shape = BurstShape.Ring, BurstTimeSeconds = 1f, ParticleCount = 12 };

        IReadOnlyList<ParticleSnapshot> atBurst = FireworkSimulator.Sample(effect, new FireCue(), 1f);

        float expectedHeight = atBurst[0].Position.Y;
        Assert.All(atBurst, particle => Assert.Equal(expectedHeight, particle.Position.Y, precision: 4));
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
}
