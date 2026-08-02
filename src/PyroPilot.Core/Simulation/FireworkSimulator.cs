using System.Numerics;
using PyroPilot.Core.Model;

namespace PyroPilot.Core.Simulation;

/// <summary>
/// Deterministically evaluates an effect from absolute elapsed time. It keeps no
/// mutable clock state, which makes pause, replay, and arbitrary timeline seeks exact.
/// </summary>
public static class FireworkSimulator
{
    public static IReadOnlyList<ParticleSnapshot> Sample(
        FireworkEffect effect,
        FireCue cue,
        float elapsedSeconds)
    {
        if (elapsedSeconds < 0) return [];

        Vector3 origin = new(cue.LaunchPosition.X, cue.LaunchPosition.Y, cue.LaunchPosition.Z);
        Vector3 launchVelocity = CreateLaunchVelocity(effect.LaunchSpeed, cue.HeadingDegrees, cue.TiltDegrees);

        if (elapsedSeconds < effect.BurstTimeSeconds)
        {
            var comet = new List<ParticleSnapshot>();
            const int trailSamples = 10;
            for (int trailIndex = trailSamples; trailIndex >= 0; trailIndex--)
            {
                float sampleAge = elapsedSeconds - trailIndex * 0.035f;
                if (sampleAge < 0) continue;
                Vector3 position = BallisticPosition(origin, launchVelocity, effect.Gravity, sampleAge);
                float brightness = 1f - trailIndex / (float)(trailSamples + 1);
                comet.Add(new ParticleSnapshot(position, brightness, trailIndex == 0 ? 0.2f : 0.08f, FirstColor(effect), ParticleKind.Comet));
            }
            return comet;
        }

        Vector3 burstOrigin = BallisticPosition(origin, launchVelocity, effect.Gravity, effect.BurstTimeSeconds);
        IReadOnlyList<ParticleEffectLayer> layers = effect.Layers.Count > 0
            ? effect.Layers
            : [new ParticleEffectLayer
            {
                Shape = effect.Shape,
                Speed = effect.BurstSpeed,
                LifetimeSeconds = effect.ParticleLifetimeSeconds,
                ParticleCount = effect.ParticleCount,
                Gravity = effect.Gravity,
                Drag = effect.Drag,
                Colors = effect.Colors,
            }];

        var particles = new List<ParticleSnapshot>();
        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            ParticleEffectLayer layer = layers[layerIndex];
            float layerAge = elapsedSeconds - effect.BurstTimeSeconds - layer.DelaySeconds;
            if (layerAge < 0 || layerAge >= layer.LifetimeSeconds || layer.ParticleCount <= 0) continue;

            int layerSeed = unchecked(cue.SimulationSeed * 397 + layerIndex * 7919);
            var random = new Random(layerSeed);
            for (int i = 0; i < layer.ParticleCount; i++)
            {
                Vector3 direction = CreateDirection(layer.Shape, random, i, layer.ParticleCount);
                float speedVariation = 0.82f + (float)random.NextDouble() * 0.36f;
                Vector3 initialVelocity = direction * layer.Speed * speedVariation;
                string color = layer.Colors.Length == 0 ? "#FFFFFF" : layer.Colors[i % layer.Colors.Length];

                int samples = Math.Max(0, layer.TrailSamples);
                for (int trailIndex = samples; trailIndex >= 0; trailIndex--)
                {
                    float sampleAge = layerAge - trailIndex * Math.Max(0.005f, layer.TrailSpacingSeconds);
                    if (sampleAge < 0) continue;
                    float dragScale = MathF.Exp(-Math.Max(0, layer.Drag) * sampleAge);
                    Vector3 position = BallisticPosition(burstOrigin, initialVelocity * dragScale, layer.Gravity, sampleAge);
                    float lifeFade = Math.Clamp(1f - sampleAge / layer.LifetimeSeconds, 0f, 1f);
                    float trailFade = 1f - trailIndex / (float)(samples + 1);
                    float twinkle = layer.Twinkle <= 0
                        ? 1f
                        : 0.55f + 0.45f * MathF.Sin((sampleAge * 34f + i * 1.7f) * layer.Twinkle);
                    float brightness = Math.Clamp(lifeFade * trailFade * twinkle, 0f, 1f);
                    float size = trailIndex == 0 ? 0.12f : 0.07f;
                    particles.Add(new ParticleSnapshot(position, brightness, size, color, ParticleKind.Spark));
                }
            }
        }

        return particles;
    }

    private static Vector3 CreateLaunchVelocity(float speed, float headingDegrees, float tiltDegrees)
    {
        float heading = DegreesToRadians(headingDegrees);
        float tilt = DegreesToRadians(tiltDegrees);
        float horizontal = speed * MathF.Sin(tilt);
        return new Vector3(horizontal * MathF.Sin(heading), speed * MathF.Cos(tilt), horizontal * MathF.Cos(heading));
    }

    private static Vector3 CreateDirection(BurstShape shape, Random random, int index, int count)
    {
        if (shape == BurstShape.Ring)
        {
            float angle = MathF.Tau * index / count;
            return new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        }

        double y = random.NextDouble() * 2.0 - 1.0;
        double angleAround = random.NextDouble() * Math.Tau;
        double radius = Math.Sqrt(Math.Max(0, 1.0 - y * y));
        Vector3 sphere = new((float)(radius * Math.Cos(angleAround)), (float)y, (float)(radius * Math.Sin(angleAround)));

        return shape switch
        {
            BurstShape.Palm => Vector3.Normalize(new Vector3(sphere.X * 0.55f, MathF.Abs(sphere.Y) + 0.25f, sphere.Z * 0.55f)),
            BurstShape.Chrysanthemum => Vector3.Normalize(new Vector3(sphere.X, sphere.Y * 0.85f, sphere.Z)),
            _ => sphere,
        };
    }

    private static Vector3 BallisticPosition(Vector3 origin, Vector3 velocity, float gravity, float seconds) =>
        origin + velocity * seconds + new Vector3(0, -0.5f * gravity * seconds * seconds, 0);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

    private static string FirstColor(FireworkEffect effect) =>
        effect.Colors.Length == 0 ? "#FFFFFF" : effect.Colors[0];
}
