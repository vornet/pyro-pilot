using System.Net;
using System.Net.Sockets;
using System.Text;
using Firefly.Client.Protocol.Mesh;

namespace Firefly.MockDevice;

/// <summary>
/// Fakes a FireFly Plus mesh (V2) device family for local development: accepts
/// TCP connections on the mesh port and answers every command
/// <see cref="MeshCommandBuilder"/> can issue with a structurally valid,
/// checksummed <see cref="MeshFrame"/> response. Reuses <see cref="MeshFrame"/>
/// for both decoding requests and encoding responses since the wire format is
/// symmetric -- only the "command"-vs-"status" meaning of one byte differs by
/// direction, and <see cref="Firefly.Client.FireflyMeshClient"/> never inspects
/// the frame's source/destination address fields, so this doesn't need to
/// reproduce a real device's addressing to be a faithful enough test double.
/// </summary>
public sealed class MeshMockServer
{
    private readonly int _port;
    private readonly List<ushort> _deviceIds;
    private readonly Dictionary<ushort, int[]> _cueTables = new();

    public MeshMockServer(int port, int deviceCount)
    {
        _port = port;
        _deviceIds = Enumerable.Range(1, Math.Max(1, deviceCount))
            .Select(i => (ushort)(0x0100 + i))
            .ToList();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[mesh] Listening on 0.0.0.0:{_port} -- {_deviceIds.Count} simulated device(s): {string.Join(", ", _deviceIds.Select(id => id.ToString("X4")))}");
        Console.WriteLine("[mesh] Ctrl+C to stop.");

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
        Console.WriteLine($"[mesh] Client connected from {client.Client.RemoteEndPoint}.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[]? request = await ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request is null) break;

                MeshFrame frame = MeshFrame.Decode(request);
                byte[] response = BuildResponse(frame);
                if (response.Length > 0)
                    await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
        {
            // Client disconnected or the server is shutting down -- not worth logging as an error.
        }
        finally
        {
            Console.WriteLine("[mesh] Client disconnected.");
        }
    }

    private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[7];
        if (!await TryReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false)) return null;

        byte declaredLength = header[6];
        var rest = new byte[declaredLength + 2];
        if (!await TryReadExactAsync(stream, rest, cancellationToken).ConfigureAwait(false)) return null;

        var full = new byte[header.Length + rest.Length];
        header.CopyTo(full, 0);
        rest.CopyTo(full, header.Length);
        return full;
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

    private byte[] BuildResponse(MeshFrame frame)
    {
        var command = (MeshCommand)frame.StatusOrCommand;
        Console.WriteLine($"[mesh] <- {command} dest={frame.DestinationAddress:X4} data={Convert.ToHexString(frame.Data)}");

        bool success = true;
        byte[] data = [];

        switch (command)
        {
            case MeshCommand.Login:
            case MeshCommand.StartAutoFire:
            case MeshCommand.StopAutoFire:
            case MeshCommand.DeletePlan:
            case MeshCommand.ClearPlan:
            case MeshCommand.StartPlan:
            case MeshCommand.FlashLed:
            case MeshCommand.ModifyMeshParam:
            case MeshCommand.AutoIgnite:
                break;

            case MeshCommand.MeshList:
                data = BuildMeshListPayload();
                break;

            case MeshCommand.PortStatus:
                // Big-endian 16-bit continuity mask. Bit 0 is the reserved
                // always-on status bit; bits 1-15 correspond to ports 1-15.
                // The mock starts with no fuses connected.
                data = [0x00, 0x01];
                break;

            case MeshCommand.ManualFire:
                success = frame.Data.Length >= 1 && frame.Data[0] is >= 1 and <= MeshCommandBuilder.PortCount;
                break;

            case MeshCommand.AddPlanFire:
                success = TryStoreCueTable(frame.DestinationAddress, frame.Data);
                break;

            case MeshCommand.DeviceInfo:
                data = Encoding.UTF8.GetBytes("PyroPilot mock mesh device -- firmware 2025-06-01");
                break;

            default:
                Console.WriteLine($"[mesh] Unrecognized command byte 0x{frame.StatusOrCommand:X2}; ignoring.");
                return [];
        }

        byte statusByte = success ? MeshCommandCodes.Success(command) : MeshCommandCodes.Failure(command);
        byte[] response = MeshFrame.Encode(MeshFrame.AppSourceAddress, (MeshCommand)statusByte, data);
        Console.WriteLine($"[mesh] -> {command} {(success ? "ACK" : "NAK")} (0x{statusByte:X2})");
        return response;
    }

    private byte[] BuildMeshListPayload()
    {
        // Record layout expected by MeshResponseParser.ParseMeshDeviceIds: byte 0
        // is skipped, then each device is a 5-byte record (2-byte id + 3 filler).
        var payload = new byte[1 + _deviceIds.Count * 5];
        payload[0] = (byte)_deviceIds.Count;
        for (int i = 0; i < _deviceIds.Count; i++)
        {
            int offset = 1 + i * 5;
            payload[offset] = (byte)(_deviceIds[i] >> 8);
            payload[offset + 1] = (byte)_deviceIds[i];
        }
        return payload;
    }

    private bool TryStoreCueTable(ushort deviceId, byte[] data)
    {
        if (data.Length != 1 + MeshCommandBuilder.PortCount * 3) return false;

        var times = new int[MeshCommandBuilder.PortCount];
        for (int i = 0; i < MeshCommandBuilder.PortCount; i++)
        {
            int offset = 1 + i * 3;
            times[i] = (data[offset] << 8) | data[offset + 1];
        }

        ushort key = deviceId == MeshFrame.BroadcastAddress && _deviceIds.Count > 0 ? _deviceIds[0] : deviceId;
        _cueTables[key] = times;
        return true;
    }
}
