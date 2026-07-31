using System.Net;
using System.Net.Sockets;
using Firefly.Client.Protocol.Single;

namespace Firefly.MockDevice;

/// <summary>
/// Fakes a standalone FireFly (V3 single-device) device for local development.
/// See the trailing-byte handling in <see cref="ReadFrameAsync"/> for the one
/// wrinkle in this protocol: <see cref="SingleCommandBuilder.Login"/> sends one
/// byte more than its own declared length accounts for.
/// </summary>
public sealed class SingleMockServer
{
    private readonly int _port;
    private readonly Dictionary<byte, ushort> _cueTable = new();

    public SingleMockServer(int port)
    {
        _port = port;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[single] Listening on 0.0.0.0:{_port}. Ctrl+C to stop.");

        await using var registration = cancellationToken.Register(listener.Stop);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    break;
                }

                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        client.NoDelay = true;
        NetworkStream stream = client.GetStream();
        Console.WriteLine($"[single] Client connected from {client.Client.RemoteEndPoint}.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[]? payload = await ReadFrameAsync(client, stream, cancellationToken).ConfigureAwait(false);
                if (payload is null) break;

                byte[] response = BuildResponse(payload);
                await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
        {
            // Client disconnected or the server is shutting down -- not worth logging as an error.
        }
        finally
        {
            Console.WriteLine("[single] Client disconnected.");
        }
    }

    /// <summary>Reads one request frame's payload (everything after the 0xAA/length header).</summary>
    private static async Task<byte[]?> ReadFrameAsync(TcpClient client, NetworkStream stream, CancellationToken cancellationToken)
    {
        var startAndLength = new byte[2];
        if (!await TryReadExactAsync(stream, startAndLength, cancellationToken).ConfigureAwait(false)) return null;
        if (startAndLength[0] != SingleFrame.StartByte)
        {
            // Desynced stream -- bail out and let the caller drop the connection rather than misparse forever.
            return null;
        }

        byte payloadLength = startAndLength[1];
        var payload = new byte[payloadLength];
        if (!await TryReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false)) return null;

        // SingleCommandBuilder.Login appends one extra byte the length field
        // doesn't account for. Drain it here so it doesn't get misread as the
        // start of the next frame.
        bool isLogin = payloadLength >= 5 && payload[0] == SingleFrame.LoginFrameType;
        if (isLogin)
        {
            await Task.Delay(15, cancellationToken).ConfigureAwait(false);
            while (client.Available > 0)
            {
                var discard = new byte[client.Available];
                await stream.ReadAsync(discard, cancellationToken).ConfigureAwait(false);
            }
        }

        return payload;
    }

    private static async Task<bool> TryReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (n == 0) return false;
            read += n;
        }
        return true;
    }

    private byte[] BuildResponse(byte[] payload)
    {
        // payload layout: [frameType(1)] [passcode(3)] [command(1)] [data...]
        if (payload.Length < 5)
        {
            Console.WriteLine($"[single] Malformed payload ({payload.Length} byte(s)); NAK.");
            return [SingleFrame.ResponseStartByte, 0x00, 0x00, 0x00];
        }

        byte commandByte = payload[4];
        var command = (SingleCommand)commandByte;
        byte[] data = payload[5..];

        bool success = true;
        switch (command)
        {
            case SingleCommand.Login:
            case SingleCommand.Fire:
            case SingleCommand.Test:
            case SingleCommand.Status:
                break;

            case SingleCommand.ManualFire:
                success = data.Length >= 1 && data[0] is >= 1 and <= 15;
                break;

            case SingleCommand.Cue:
                success = data.Length >= 3 && data[0] is >= 1 and <= 15;
                if (success) _cueTable[data[0]] = (ushort)((data[1] << 8) | data[2]);
                break;

            default:
                success = false;
                break;
        }

        Console.WriteLine($"[single] <- {command} data={Convert.ToHexString(data)} => {(success ? "ACK" : "NAK")}");
        return [SingleFrame.ResponseStartByte, commandByte, (byte)(success ? 0x01 : 0x00), 0x00];
    }
}
