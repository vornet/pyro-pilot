using Firefly.Client.Models;
using Firefly.Client.Protocol.Single;
using Firefly.Client.Transport;

namespace Firefly.Client;

/// <summary>
/// Client for the V3 single-device WiFi protocol used by standalone FireFly
/// devices (SSID contains "GT2404", AP at 192.168.4.1:80). This is the same
/// command encoding the app uses over classic Bluetooth for V1/V3-BLE
/// devices, just carried over a TCP socket instead of a GATT characteristic.
///
/// See the response-shape caveat on <see cref="SingleResponse"/>: response
/// parsing beyond the success/failure flag is a best-effort reconstruction
/// and should be validated against real hardware, especially
/// <see cref="GetStatusAsync"/>.
/// </summary>
public sealed class FireflySingleClient : IAsyncDisposable
{
    public const string DefaultHost = "192.168.4.1";
    public const int DefaultPort = 80;

    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(15);

    private readonly IFireflyTransport _transport;
    private readonly TimeSpan _readTimeout;
    private readonly SemaphoreSlim _exchangeLock = new(1, 1);

    public bool IsConnected => _transport.IsConnected;

    public FireflySingleClient(string host = DefaultHost, int port = DefaultPort, TimeSpan? readTimeout = null)
        : this(new FireflyTcpTransport(host, port), readTimeout)
    {
    }

    public FireflySingleClient(IFireflyTransport transport, TimeSpan? readTimeout = null)
    {
        _transport = transport;
        _readTimeout = readTimeout ?? DefaultReadTimeout;
    }

    public Task ConnectAsync(TimeSpan? connectTimeout = null, CancellationToken cancellationToken = default) =>
        _transport.ConnectAsync(connectTimeout ?? DefaultConnectTimeout, cancellationToken);

    public void Disconnect() => _transport.Disconnect();

    public Task<SingleResponse> LoginAsync(CancellationToken cancellationToken = default) =>
        ExchangeAsync(SingleCommandBuilder.Login(), SingleCommand.Login, cancellationToken);

    public Task<SingleResponse> ManualFireAsync(byte port, CancellationToken cancellationToken = default) =>
        ExchangeAsync(SingleCommandBuilder.ManualFire(port), SingleCommand.ManualFire, cancellationToken);

    public Task<SingleResponse> WriteCueAsync(byte port, ushort durationMs, CancellationToken cancellationToken = default) =>
        ExchangeAsync(SingleCommandBuilder.Cue(port, durationMs), SingleCommand.Cue, cancellationToken);

    public Task<SingleResponse> FireAsync(CancellationToken cancellationToken = default) =>
        ExchangeAsync(SingleCommandBuilder.Fire(), SingleCommand.Fire, cancellationToken);

    public Task<SingleResponse> TestAsync(CancellationToken cancellationToken = default) =>
        ExchangeAsync(SingleCommandBuilder.Test(), SingleCommand.Test, cancellationToken);

    public Task<SingleResponse> GetStatusAsync(CancellationToken cancellationToken = default) =>
        ExchangeAsync(SingleCommandBuilder.Status(), SingleCommand.Status, cancellationToken);

    /// <summary>
    /// Convenience helper to program all 15 ports in sequence, since the app
    /// uploads a show's cue table one port at a time rather than in a single
    /// frame (unlike the mesh protocol's AddPlanFire). Stops at the first
    /// failed write; inspect the returned list to see how far it got.
    /// </summary>
    public async Task<IReadOnlyList<SingleResponse>> WriteCueTableAsync(
        IReadOnlyList<(byte Port, ushort DurationMs)> cues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var results = new List<SingleResponse>(cues.Count);
        foreach (var (port, durationMs) in cues)
        {
            var response = await WriteCueAsync(port, durationMs, cancellationToken).ConfigureAwait(false);
            results.Add(response);
            if (!response.IsSuccess) break;
        }
        return results;
    }

    private async Task<SingleResponse> ExchangeAsync(byte[] request, SingleCommand command, CancellationToken cancellationToken)
    {
        await _exchangeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            byte[] raw = await _transport.ReadExactAsync(4, _readTimeout, cancellationToken).ConfigureAwait(false);
            return SingleResponse.Decode(command, raw);
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
