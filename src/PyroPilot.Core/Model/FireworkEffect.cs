namespace PyroPilot.Core.Model;

public enum BurstShape
{
    Peony,
    Chrysanthemum,
    Ring,
    Palm,
}

/// <summary>
/// Renderer-independent description of a firework effect. Values use seconds,
/// metres, and metres per second so renderers do not depend on UI pixel sizes.
/// </summary>
public sealed class FireworkEffect
{
    public BurstShape Shape { get; set; } = BurstShape.Peony;
    public float BurstTimeSeconds { get; set; } = 1.8f;
    public float LaunchSpeed { get; set; } = 42f;
    public float BurstSpeed { get; set; } = 18f;
    public float ParticleLifetimeSeconds { get; set; } = 2.4f;
    public int ParticleCount { get; set; } = 160;
    public float Gravity { get; set; } = 9.81f;
    public float Drag { get; set; } = 0.08f;
    public string[] Colors { get; set; } = ["#FF7A00"];

    /// <summary>
    /// Timed particle layers emitted from the shell break. Empty means the legacy
    /// single-layer properties above should be used for backward compatibility.
    /// </summary>
    public List<ParticleEffectLayer> Layers { get; set; } = [];
}

/// <summary>One independently configurable visual layer within a firework break.</summary>
public sealed class ParticleEffectLayer
{
    public string Name { get; set; } = "Primary Burst";
    public float DelaySeconds { get; set; }
    public BurstShape Shape { get; set; } = BurstShape.Peony;
    public float Speed { get; set; } = 18f;
    public float LifetimeSeconds { get; set; } = 2.4f;
    public int ParticleCount { get; set; } = 160;
    public float Gravity { get; set; } = 9.81f;
    public float Drag { get; set; } = 0.08f;
    public int TrailSamples { get; set; } = 5;
    public float TrailSpacingSeconds { get; set; } = 0.045f;
    public float Twinkle { get; set; }
    public string[] Colors { get; set; } = ["#FF7A00"];
}
