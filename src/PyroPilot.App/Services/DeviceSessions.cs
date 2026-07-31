using Firefly.Client;
using Firefly.Client.Protocol.Mesh;
using PyroPilot.Core.Model;

namespace PyroPilot.App.Services;

public sealed class MeshDeviceSession(string host, int port, ushort? meshDeviceId) : IDeviceSession
{
    private readonly FireflyMeshClient _client = new(host, port);
    private readonly ushort _deviceId = meshDeviceId ?? MeshFrame.BroadcastAddress;

    public bool IsConnected => _client.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _client.ConnectAsync(cancellationToken: cancellationToken);

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default) =>
        (await _client.LoginAsync(cancellationToken).ConfigureAwait(false)).IsSuccess;

    public async Task<bool> ManualFireAsync(int port, CancellationToken cancellationToken = default) =>
        (await _client.ManualFireAsync(_deviceId, (byte)port, cancellationToken).ConfigureAwait(false)).IsSuccess;

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}

public sealed class SingleDeviceSession(string host, int port) : IDeviceSession
{
    private readonly FireflySingleClient _client = new(host, port);

    public bool IsConnected => _client.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _client.ConnectAsync(cancellationToken: cancellationToken);

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default) =>
        (await _client.LoginAsync(cancellationToken).ConfigureAwait(false)).IsSuccess;

    public async Task<bool> ManualFireAsync(int port, CancellationToken cancellationToken = default) =>
        (await _client.ManualFireAsync((byte)port, cancellationToken).ConfigureAwait(false)).IsSuccess;

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}

public static class DeviceSessionFactory
{
    public static IDeviceSession Create(PairedDevice device) => device.Protocol switch
    {
        DeviceProtocol.Mesh => new MeshDeviceSession(device.Host, device.Port, device.MeshDeviceId),
        DeviceProtocol.Single => new SingleDeviceSession(device.Host, device.Port),
        _ => throw new ArgumentOutOfRangeException(nameof(device), device.Protocol, "Unknown device protocol."),
    };
}
