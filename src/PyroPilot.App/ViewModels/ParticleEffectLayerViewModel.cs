using CommunityToolkit.Mvvm.ComponentModel;
using PyroPilot.Core.Model;

namespace PyroPilot.App.ViewModels;

public partial class ParticleEffectLayerViewModel : ViewModelBase
{
    public static IReadOnlyList<BurstShape> Shapes { get; } = Enum.GetValues<BurstShape>();

    [ObservableProperty] private string _name = "Primary Burst";
    [ObservableProperty] private float _delaySeconds;
    [ObservableProperty] private BurstShape _shape = BurstShape.Peony;
    [ObservableProperty] private float _speed = 18f;
    [ObservableProperty] private float _lifetimeSeconds = 2.4f;
    [ObservableProperty] private int _particleCount = 160;
    [ObservableProperty] private float _gravity = 9.81f;
    [ObservableProperty] private float _drag = 0.08f;
    [ObservableProperty] private int _trailSamples = 5;
    [ObservableProperty] private float _trailSpacingSeconds = 0.045f;
    [ObservableProperty] private float _twinkle;
    [ObservableProperty] private string _colorsText = "#FF7A00";

    public static ParticleEffectLayerViewModel FromModel(ParticleEffectLayer layer) => new()
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
        ColorsText = string.Join(", ", layer.Colors),
    };

    public ParticleEffectLayer ToModel() => new()
    {
        Name = Name,
        DelaySeconds = Math.Max(0, DelaySeconds),
        Shape = Shape,
        Speed = Math.Max(0, Speed),
        LifetimeSeconds = Math.Max(0.05f, LifetimeSeconds),
        ParticleCount = Math.Clamp(ParticleCount, 1, 5000),
        Gravity = Gravity,
        Drag = Math.Max(0, Drag),
        TrailSamples = Math.Clamp(TrailSamples, 0, 30),
        TrailSpacingSeconds = Math.Clamp(TrailSpacingSeconds, 0.005f, 0.5f),
        Twinkle = Math.Clamp(Twinkle, 0, 4),
        Colors = ColorsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    };
}
