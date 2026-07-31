using Firefly.Client.Models;
using Firefly.Client.Protocol.Mesh;
using Firefly.Client.Transport;

namespace Firefly.Client;

/// <summary>
/// Client for the V2 "mesh" WiFi protocol used by FireFly Plus / GT2404-A
/// devices (SSID contains "TitanFire", AP at 192.168.8.1:7008). A mesh can
/// have multiple physical devices; most commands target one by its 2-byte
/// mesh device ID (from <see cref="GetMeshListAsync"/>), or the broadcast
/// address 0xFFFF for the responding master.
///
/// Equivalent to the original app's TCP connection and mesh command
/// handling, but with request/response matched per call instead of via a
/// shared mutable listener, and with frame-length-aware reads instead of the
/// app's "poll Available() until quiet" loop.
/// </summary>
public sealed class FireflyMeshClient : IAsyncDisposable
{
    public const string DefaultHost = "192.168.8.1";
    public const int DefaultPort = 7008;

    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(30);

    private readonly IFireflyTransport _transport;
    private readonly TimeSpan _readTimeout;
    private readonly SemaphoreSlim _exchangeLock = new(1, 1);

    /// <summary>Fire-delay mode applied to timing commands that don't take an explicit mode. Mirrors the app's UI toggle.</summary>
    public FireDelayMode FireDelayMode { get; set; } = FireDelayMode.Normal;

    public bool IsConnected => _transport.IsConnected;

    public FireflyMeshClient(string host = DefaultHost, int port = DefaultPort, TimeSpan? readTimeout = null)
        : this(new FireflyTcpTransport(host, port), readTimeout)
    {
    }

    /// <summary>Test/advanced-use constructor accepting a custom transport (e.g. a mock or an already-connected socket wrapper).</summary>
    public FireflyMeshClient(IFireflyTransport transport, TimeSpan? readTimeout = null)
    {
        _transport = transport;
        _readTimeout = readTimeout ?? DefaultReadTimeout;
    }

    public Task ConnectAsync(TimeSpan? connectTimeout = null, CancellationToken cancellationToken = default) =>
        _transport.ConnectAsync(connectTimeout ?? DefaultConnectTimeout, cancellationToken);

    public void Disconnect() => _transport.Disconnect();

    public Task<MeshResponse> LoginAsync(CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.Login(), MeshCommand.Login, cancellationToken);

    public async Task<IReadOnlyList<ushort>> GetMeshListAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExchangeAsync(MeshCommandBuilder.MeshList(), MeshCommand.MeshList, cancellationToken).ConfigureAwait(false);
        return MeshResponseParser.ParseMeshDeviceIds(response.Data);
    }

    public Task<MeshResponse> GetPortStatusAsync(ushort deviceId = MeshFrame.BroadcastAddress, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.PortStatus(deviceId), MeshCommand.PortStatus, cancellationToken);

    public Task<MeshResponse> ManualFireAsync(ushort deviceId, byte port, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.ManualFire(deviceId, port, FireDelayMode), MeshCommand.ManualFire, cancellationToken);

    public Task<MeshResponse> StartAutoFireAsync(ushort deviceId, ushort data = 0xFFFF, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.StartAutoFire(deviceId, data), MeshCommand.StartAutoFire, cancellationToken);

    public Task<MeshResponse> StopAutoFireAsync(ushort deviceId, ushort data = 0xFFFF, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.StopAutoFire(deviceId, data), MeshCommand.StopAutoFire, cancellationToken);

    /// <summary>Programs a device's 15-port fire-time table. See <see cref="MeshCommandBuilder.AddPlanFire"/>.</summary>
    public Task<MeshResponse> WriteCueTableAsync(ushort deviceId, IReadOnlyList<int> portTimesMs, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.AddPlanFire(deviceId, portTimesMs, FireDelayMode), MeshCommand.AddPlanFire, cancellationToken);

    public Task<MeshResponse> DeletePlanAsync(ushort deviceId, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.DeletePlan(deviceId), MeshCommand.DeletePlan, cancellationToken);

    public Task<MeshResponse> StartPlanAsync(ushort deviceId = MeshFrame.BroadcastAddress, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.StartPlan(deviceId), MeshCommand.StartPlan, cancellationToken);

    public Task<MeshResponse> ClearPlanAsync(ushort deviceId, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.ClearPlan(deviceId), MeshCommand.ClearPlan, cancellationToken);

    public Task<MeshResponse> FlashLedAsync(ushort deviceId, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.FlashLed(deviceId), MeshCommand.FlashLed, cancellationToken);

    /// <summary>
    /// Changes the device's WiFi SSID/password. The device will apply the new
    /// credentials and drop the current connection -- callers need to
    /// reconnect (and re-associate the host WiFi radio) using the new SSID.
    /// </summary>
    public Task<MeshResponse> ModifyMeshParamAsync(ushort deviceId, string newSsid, string newPassword, CancellationToken cancellationToken = default) =>
        ExchangeAsync(MeshCommandBuilder.ModifyMeshParam(deviceId, newSsid, newPassword), MeshCommand.ModifyMeshParam, cancellationToken);

    public async Task<MeshDeviceInfo> GetDeviceInfoAsync(ushort deviceId = MeshFrame.BroadcastAddress, CancellationToken cancellationToken = default)
    {
        var response = await ExchangeAsync(MeshCommandBuilder.DeviceInfo(deviceId), MeshCommand.DeviceInfo, cancellationToken).ConfigureAwait(false);
        string? firmwareDate = MeshResponseParser.ParseFirmwareDate(response.RawFrame);
        return new MeshDeviceInfo(firmwareDate, response);
    }

    /// <summary>
    /// Sends a request and reads back exactly one frame-length-aware response.
    /// Only one exchange is in flight at a time per client (the protocol is a
    /// strict request/response ping-pong over a single socket, same as the app).
    /// </summary>
    private async Task<MeshResponse> ExchangeAsync(byte[] request, MeshCommand command, CancellationToken cancellationToken)
    {
        await _exchangeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);

            byte[] header = await _transport.ReadExactAsync(7, _readTimeout, cancellationToken).ConfigureAwait(false);
            byte declaredLength = header[6];
            byte[] rest = await _transport.ReadExactAsync(declaredLength + 2, _readTimeout, cancellationToken).ConfigureAwait(false);

            byte[] full = new byte[header.Length + rest.Length];
            header.CopyTo(full, 0);
            rest.CopyTo(full, header.Length);

            MeshFrame frame = MeshFrame.Decode(full);
            bool recognized = MeshCommandCodes.TryGetCommand(frame.StatusOrCommand, out _, out bool isSuccess);

            return new MeshResponse(command, recognized && isSuccess, recognized, frame.ChecksumValid, frame.StatusOrCommand, frame.Data, full);
        }
        finally
        {
            _exchangeLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _exchangeLock.Dispose();
        return _transport.DisposeAsync();
    }
}
