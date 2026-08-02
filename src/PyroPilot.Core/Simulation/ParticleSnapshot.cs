using System.Numerics;

namespace PyroPilot.Core.Simulation;

public enum ParticleKind
{
    Comet,
    Spark,
}

/// <summary>One renderer-neutral particle evaluated at an exact point in show time.</summary>
public readonly record struct ParticleSnapshot(
    Vector3 Position,
    float Brightness,
    float Size,
    string ColorHex,
    ParticleKind Kind);
