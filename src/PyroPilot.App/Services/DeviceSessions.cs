using Firefly.Client;
using Firefly.Client.Protocol.Mesh;
using PyroPilot.Core.Model;

namespace PyroPilot.App.Services;

public sealed class MeshDeviceSession(string host, int port, ushort? meshDeviceId) : IDeviceSession
{
    private readonly FireflyMeshClient _client = new(host, port);
    private readonly ushort _deviceId = meshDeviceId ?? MeshFrame.BroadcastAddress;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public bool IsConnected => _client.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _client.ConnectAsync(cancellationToken: cancellationToken);

    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await _client.LoginAsync(cancellationToken).ConfigureAwait(false)).IsSuccess;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<bool> ManualFireAsync(int port, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await _client.ManualFireAsync(_deviceId, (byte)port, cancellationToken).ConfigureAwait(false)).IsSuccess;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PortContinuityStatus?> TryReadPortContinuityAsync(CancellationToken cancellationToken = default)
    {
        if (!await _operationLock.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return null;
        try
        {
            var response = await _client.GetPortStatusAsync(_deviceId, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess || !response.ChecksumValid) return null;
            return new PortContinuityStatus(MeshResponseParser.ParseConnectedPorts(response.Data).ToHashSet());
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
        _operationLock.Dispose();
    }
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

    public Task<PortContinuityStatus?> TryReadPortContinuityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<PortContinuityStatus?>(null);

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
