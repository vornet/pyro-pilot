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
                float speedVariation = layer.Shape switch
                {
                    BurstShape.Dahlia => 0.72f + (float)random.NextDouble() * 0.56f,
                    BurstShape.Willow => 0.88f + (float)random.NextDouble() * 0.22f,
                    _ => 0.82f + (float)random.NextDouble() * 0.36f,
                };
                Vector3 initialVelocity = direction * layer.Speed * speedVariation;
                string color = layer.Colors.Length == 0 ? "#FFFFFF" : layer.Colors[i % layer.Colors.Length];

                int samples = Math.Max(0, layer.TrailSamples);
                for (int trailIndex = samples; trailIndex >= 0; trailIndex--)
                {
                    float sampleAge = layerAge - trailIndex * Math.Max(0.005f, layer.TrailSpacingSeconds);
                    if (sampleAge < 0) continue;
                    Vector3 position = DraggedBallisticPosition(burstOrigin, initialVelocity, layer.Gravity, layer.Drag, sampleAge);
                    float normalizedAge = Math.Clamp(layerAge / layer.LifetimeSeconds, 0f, 1f);
                    // Stars stay hot through most of their flight, then extinguish
                    // quickly. This reads much more like burning composition than a
                    // uniformly fading computer particle.
                    float lifeFade = 1f - SmoothStep(0.72f, 1f, normalizedAge);
                    // Historical samples must remain subordinate to the live star
                    // head. With additive blending, equally bright histories pile
                    // up near the burst origin and make expansion read backwards.
                    float trailProgress = 1f - trailIndex / (float)(samples + 1);
                    float trailFade = trailIndex == 0
                        ? 1f
                        : 0.34f * MathF.Pow(trailProgress, 1.65f);
                    float twinkle = layer.Twinkle <= 0
                        ? 1f
                        : 0.58f + 0.42f * MathF.Sin((sampleAge * 28f + i * 2.17f) * layer.Twinkle);
                    float brightness = Math.Clamp(lifeFade * trailFade * twinkle, 0f, 1f);
                    float size = trailIndex == 0 ? layer.SparkSize : layer.TrailSize;
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
            // Face the authored ring toward the default preview camera. A ring in
            // the horizontal X/Z plane is viewed edge-on and looks like a line.
            return new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0.08f * MathF.Sin(angle * 3f));
        }

        if (shape == BurstShape.Palm)
        {
            const int arms = 10;
            int arm = index % arms;
            float angle = MathF.Tau * arm / arms + ((float)random.NextDouble() - 0.5f) * 0.09f;
            float horizontal = 0.43f + (float)random.NextDouble() * 0.12f;
            float rise = 0.78f + (float)random.NextDouble() * 0.16f;
            return Vector3.Normalize(new Vector3(MathF.Cos(angle) * horizontal, rise, MathF.Sin(angle) * horizontal));
        }

        // A Fibonacci sphere avoids the lumpy clusters produced by purely random
        // samples while a little seeded jitter keeps it organic.
        double y = 1.0 - 2.0 * (index + 0.5) / Math.Max(1, count);
        double angleAround = index * 2.399963229728653 + (random.NextDouble() - 0.5) * 0.16;
        double radius = Math.Sqrt(Math.Max(0, 1.0 - y * y));
        Vector3 sphere = new((float)(radius * Math.Cos(angleAround)), (float)y, (float)(radius * Math.Sin(angleAround)));

        return shape switch
        {
            BurstShape.Chrysanthemum => Vector3.Normalize(new Vector3(sphere.X, sphere.Y * 0.85f, sphere.Z)),
            BurstShape.Willow => Vector3.Normalize(new Vector3(sphere.X, sphere.Y * 0.62f + 0.22f, sphere.Z)),
            BurstShape.Dahlia => Vector3.Normalize(new Vector3(sphere.X, sphere.Y * 1.08f, sphere.Z)),
            _ => sphere,
        };
    }

    private static Vector3 DraggedBallisticPosition(Vector3 origin, Vector3 velocity, float gravity, float drag, float seconds)
    {
        float clampedDrag = Math.Max(0, drag);
        float travel = clampedDrag < 0.0001f
            ? seconds
            : (1f - MathF.Exp(-clampedDrag * seconds)) / clampedDrag;
        return origin + velocity * travel + new Vector3(0, -0.5f * gravity * seconds * seconds, 0);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Vector3 BallisticPosition(Vector3 origin, Vector3 velocity, float gravity, float seconds) =>
        origin + velocity * seconds + new Vector3(0, -0.5f * gravity * seconds * seconds, 0);

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

    private static string FirstColor(FireworkEffect effect) =>
        effect.Colors.Length == 0 ? "#FFFFFF" : effect.Colors[0];
}
