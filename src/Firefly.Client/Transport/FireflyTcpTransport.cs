using System.Net.Sockets;

namespace Firefly.Client.Transport;

/// <summary>
/// Plain TCP socket transport to a FireFly device's SoftAP, equivalent to the
/// java.net.Socket usage in the original app's networking layer: no TLS,
/// keep-alive enabled, address reuse enabled.
/// </summary>
public sealed class FireflyTcpTransport : IFireflyTransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public FireflyTcpTransport(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _host = host;
        _port = port;
    }

    public bool IsConnected => _client is { Connected: true };

    public async Task ConnectAsync(TimeSpan connectTimeout, CancellationToken cancellationToken = default)
    {
        Disconnect();

        var client = new TcpClient();
        try
        {
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            using var timeoutCts = new CancellationTokenSource(connectTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await client.ConnectAsync(_host, _port, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out connecting to {_host}:{_port} after {connectTimeout}.");
            }

            client.NoDelay = true;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        _client = client;
        _stream = client.GetStream();
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _stream!.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadExactAsync(int count, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (count == 0) return [];

        var buffer = new byte[count];
        var read = 0;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            while (read < count)
            {
                int n = await _stream!.ReadAsync(buffer.AsMemory(read, count - read), linkedCts.Token).ConfigureAwait(false);
                if (n == 0)
                    throw new IOException("The device closed the connection while a response was still expected.");
                read += n;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out after {timeout} waiting for {count} response byte(s) from the device (received {read}).");
        }

        return buffer;
    }

    public async Task<byte[]> ReadUntilQuietAsync(TimeSpan overallTimeout, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        using var timeoutCts = new CancellationTokenSource(overallTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var buffer = new MemoryStream();
        var singleByte = new byte[1];
        try
        {
            // Block for the first byte -- the device hasn't responded yet -- then
            // keep draining whatever has already arrived in the socket buffer
            // without blocking. A gap with nothing available is treated as end-of-frame.
            int n = await _stream!.ReadAsync(singleByte.AsMemory(0, 1), linkedCts.Token).ConfigureAwait(false);
            if (n == 0)
                throw new IOException("The device closed the connection while a response was still expected.");
            buffer.Write(singleByte, 0, 1);

            while (_client!.Available > 0)
            {
                var chunk = new byte[_client.Available];
                int r = await _stream.ReadAsync(chunk, linkedCts.Token).ConfigureAwait(false);
                buffer.Write(chunk, 0, r);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out after {overallTimeout} waiting for a response from the device.");
        }

        return buffer.ToArray();
    }

    public void Disconnect()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    private void EnsureConnected()
    {
        if (_client is not { Connected: true } || _stream is null)
            throw new InvalidOperationException("Not connected to a FireFly device. Call ConnectAsync first.");
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
