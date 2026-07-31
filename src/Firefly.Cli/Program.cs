using System.Globalization;
using System.Net.Sockets;
using Firefly.Client;
using Firefly.Client.Models;

namespace Firefly.Cli;

/// <summary>
/// Demo console app for Firefly.Client: connects to a FireFly device over
/// WiFi and demonstrates a single manual fire and a timed firing sequence,
/// against either protocol generation the library supports.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Don't hard-kill the process -- let whatever's in flight (a shot,
            // a delay) observe cancellation and stop cleanly instead of
            // leaving the socket/device in a weird state.
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Cancellation requested -- stopping after the current step...");
            cts.Cancel();
        };

        try
        {
            var reader = new ArgReader(args);
            if (reader.Positionals.Count < 2)
                throw new CliUsageException("Expected: <mesh|single> <list|manual|sequence> [options]");

            string protocol = reader.Positionals[0].ToLowerInvariant();
            string command = reader.Positionals[1].ToLowerInvariant();

            return protocol switch
            {
                "mesh" => await RunMeshAsync(command, reader, cts.Token),
                "single" => await RunSingleAsync(command, reader, cts.Token),
                _ => throw new CliUsageException($"Unknown protocol '{protocol}'; expected 'mesh' or 'single'."),
            };
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            PrintUsage();
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex) when (ex is SocketException or IOException or TimeoutException or FireflyProtocolException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Connection/protocol error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunMeshAsync(string command, ArgReader reader, CancellationToken ct)
    {
        // Parse and validate every argument up front, before opening a
        // connection -- a typo in --port shouldn't cost a 15s connect timeout
        // before it's reported.
        string host = reader.Get("host") ?? FireflyMeshClient.DefaultHost;
        int port = ParseInt(reader.Get("tcp-port"), FireflyMeshClient.DefaultPort, "--tcp-port");
        TimeSpan connectTimeout = TimeSpan.FromSeconds(ParseDouble(reader.Get("connect-timeout"), 15, "--connect-timeout"));
        bool assumeYes = reader.Flag("yes");

        if (command is not ("list" or "manual" or "sequence"))
            throw new CliUsageException($"Unknown mesh command '{command}'; expected 'list', 'manual', or 'sequence'.");

        ushort deviceId = 0xFFFF;
        byte manualPort = 0;
        IReadOnlyList<byte> sequencePorts = [];
        int delayMs = 1000;
        string confirmDescription = "";

        switch (command)
        {
            case "manual":
                deviceId = ParseDeviceId(reader.Get("device"));
                manualPort = ParsePort(reader.Require("port"));
                confirmDescription = $"manual fire, device {deviceId:X4}, port {manualPort}";
                break;
            case "sequence":
                deviceId = ParseDeviceId(reader.Get("device"));
                sequencePorts = ParsePorts(reader.Require("ports"));
                delayMs = ParseInt(reader.Get("delay"), 1000, "--delay");
                confirmDescription = $"firing sequence, device {deviceId:X4}, ports [{string.Join(", ", sequencePorts)}], {delayMs}ms apart";
                break;
        }

        await using var client = new FireflyMeshClient(host, port);
        await ConnectAndLoginAsync(client, host, port, connectTimeout, ct);

        switch (command)
        {
            case "list":
            {
                var devices = await client.GetMeshListAsync(ct);
                if (devices.Count == 0)
                {
                    Console.WriteLine("No mesh devices found.");
                }
                else
                {
                    Console.WriteLine("Mesh devices:");
                    foreach (ushort id in devices) Console.WriteLine($"  {id:X4}");
                }
                return 0;
            }

            case "manual":
            {
                if (!Confirm(confirmDescription, assumeYes)) return 1;
                var response = await client.ManualFireAsync(deviceId, manualPort, ct);
                PrintResult($"Manual fire device {deviceId:X4} port {manualPort}", response);
                return response.IsSuccess ? 0 : 1;
            }

            case "sequence":
            {
                if (!Confirm(confirmDescription, assumeYes)) return 1;
                return await FireSequenceAsync(
                    sequencePorts, delayMs,
                    (p, token) => client.ManualFireAsync(deviceId, p, token),
                    r => r.IsSuccess,
                    ct);
            }

            default:
                throw new CliUsageException($"Unknown mesh command '{command}'; expected 'list', 'manual', or 'sequence'.");
        }
    }

    private static async Task<int> RunSingleAsync(string command, ArgReader reader, CancellationToken ct)
    {
        // Parse and validate every argument up front, before opening a
        // connection -- see the comment in RunMeshAsync.
        string host = reader.Get("host") ?? FireflySingleClient.DefaultHost;
        int port = ParseInt(reader.Get("tcp-port"), FireflySingleClient.DefaultPort, "--tcp-port");
        TimeSpan connectTimeout = TimeSpan.FromSeconds(ParseDouble(reader.Get("connect-timeout"), 15, "--connect-timeout"));
        bool assumeYes = reader.Flag("yes");

        if (command is not ("manual" or "sequence"))
            throw new CliUsageException($"Unknown single command '{command}'; expected 'manual' or 'sequence'.");

        byte manualPort = 0;
        IReadOnlyList<byte> sequencePorts = [];
        int delayMs = 1000;
        string confirmDescription;

        if (command == "manual")
        {
            manualPort = ParsePort(reader.Require("port"));
            confirmDescription = $"manual fire, port {manualPort}";
        }
        else
        {
            sequencePorts = ParsePorts(reader.Require("ports"));
            delayMs = ParseInt(reader.Get("delay"), 1000, "--delay");
            confirmDescription = $"firing sequence, ports [{string.Join(", ", sequencePorts)}], {delayMs}ms apart";
        }

        await using var client = new FireflySingleClient(host, port);
        await ConnectAndLoginAsync(client, host, port, connectTimeout, ct);

        if (command == "manual")
        {
            if (!Confirm(confirmDescription, assumeYes)) return 1;
            var response = await client.ManualFireAsync(manualPort, ct);
            PrintResult($"Manual fire port {manualPort}", response);
            return response.IsSuccess ? 0 : 1;
        }
        else
        {
            if (!Confirm(confirmDescription, assumeYes)) return 1;
            return await FireSequenceAsync(
                sequencePorts, delayMs,
                (p, token) => client.ManualFireAsync(p, token),
                r => r.IsSuccess,
                ct);
        }
    }

    private static async Task ConnectAndLoginAsync(FireflyMeshClient client, string host, int port, TimeSpan connectTimeout, CancellationToken ct)
    {
        Console.WriteLine($"Connecting to mesh device at {host}:{port} ...");
        await client.ConnectAsync(connectTimeout, ct);
        Console.WriteLine("Connected. Logging in...");
        var login = await client.LoginAsync(ct);
        PrintResult("Login", login);
        if (!login.IsSuccess)
            throw new InvalidOperationException("Login failed -- check the device is powered on and the host is associated with its WiFi AP.");
    }

    private static async Task ConnectAndLoginAsync(FireflySingleClient client, string host, int port, TimeSpan connectTimeout, CancellationToken ct)
    {
        Console.WriteLine($"Connecting to single device at {host}:{port} ...");
        await client.ConnectAsync(connectTimeout, ct);
        Console.WriteLine("Connected. Logging in...");
        var login = await client.LoginAsync(ct);
        PrintResult("Login", login);
        if (!login.IsSuccess)
            throw new InvalidOperationException("Login failed -- check the device is powered on and the host is associated with its WiFi AP.");
    }

    /// <summary>
    /// Fires each port in order, waiting <paramref name="delayMs"/> between shots.
    /// Generic over the response type so it works for both the mesh (V2) and
    /// single-device (V3) clients, which return different response DTOs.
    /// </summary>
    private static async Task<int> FireSequenceAsync<TResponse>(
        IReadOnlyList<byte> ports,
        int delayMs,
        Func<byte, CancellationToken, Task<TResponse>> fireAsync,
        Func<TResponse, bool> isSuccess,
        CancellationToken ct)
    {
        Console.WriteLine($"Firing sequence: {ports.Count} port(s), {delayMs}ms between shots");
        for (int i = 0; i < ports.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            byte p = ports[i];
            Console.Write($"  [{i + 1}/{ports.Count}] port {p} ... ");

            TResponse response = await fireAsync(p, ct);
            bool ok = isSuccess(response);
            Console.WriteLine(ok ? "success" : "FAILED");

            if (!ok)
            {
                Console.Error.WriteLine("Stopping sequence after failure.");
                return 1;
            }

            if (i < ports.Count - 1)
            {
                Console.WriteLine($"  (waiting {delayMs}ms)");
                await Task.Delay(delayMs, ct);
            }
        }

        Console.WriteLine("Sequence complete.");
        return 0;
    }

    private static bool Confirm(string description, bool assumeYes)
    {
        if (assumeYes) return true;

        Console.Write($"About to fire: {description}. Type FIRE to confirm (anything else cancels): ");
        string? input = Console.ReadLine();
        if (string.Equals(input?.Trim(), "FIRE", StringComparison.Ordinal)) return true;

        Console.WriteLine("Cancelled by user.");
        return false;
    }

    private static void PrintResult(string label, MeshResponse response)
    {
        string outcome = response.IsSuccess ? "success" : response.IsRecognizedStatus ? "FAILED" : "UNRECOGNIZED";
        Console.WriteLine($"{label}: {outcome} (status 0x{response.StatusByte:X2}, checksum {(response.ChecksumValid ? "OK" : "INVALID")})");
    }

    private static void PrintResult(string label, SingleResponse response)
    {
        string outcome = response.IsSuccess ? "success" : "FAILED";
        Console.WriteLine($"{label}: {outcome} (prefix {(response.IsValidPrefix ? "OK" : "INVALID")}, echoed command 0x{response.EchoedCommand:X2})");
    }

    private static ushort ParseDeviceId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.WriteLine("No --device specified; using broadcast address FFFF.");
            return 0xFFFF;
        }

        string cleaned = raw.Trim();
        if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[2..];

        if (!ushort.TryParse(cleaned, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value))
            throw new CliUsageException($"Invalid --device value '{raw}'; expected a hex mesh device id, e.g. 0102.");
        return value;
    }

    private static byte ParsePort(string raw)
    {
        if (!byte.TryParse(raw, out byte value))
            throw new CliUsageException($"Invalid port '{raw}'; expected an integer 0-255.");
        return value;
    }

    private static IReadOnlyList<byte> ParsePorts(string raw)
    {
        string[] parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new CliUsageException("--ports must contain at least one port number, e.g. --ports 1,2,3.");
        return parts.Select(ParsePort).ToList();
    }

    private static int ParseInt(string? raw, int defaultValue, string optionName)
    {
        if (raw is null) return defaultValue;
        if (!int.TryParse(raw, out int value))
            throw new CliUsageException($"Invalid {optionName} value '{raw}'; expected an integer.");
        return value;
    }

    private static double ParseDouble(string? raw, double defaultValue, string optionName)
    {
        if (raw is null) return defaultValue;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            throw new CliUsageException($"Invalid {optionName} value '{raw}'; expected a number.");
        return value;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            firefly-cli -- demo console app for the Firefly.Client library

            Usage:
              firefly-cli mesh   list                                             [--host IP] [--tcp-port N]
              firefly-cli mesh   manual   --port N [--device HEXID]                [--host IP] [--tcp-port N] [--yes]
              firefly-cli mesh   sequence --ports N,N,N --delay MS [--device HEXID] [--host IP] [--tcp-port N] [--yes]
              firefly-cli single manual   --port N                                 [--host IP] [--tcp-port N] [--yes]
              firefly-cli single sequence --ports N,N,N --delay MS                 [--host IP] [--tcp-port N] [--yes]

            Options:
              --host IP              Override the device's default IP address
              --tcp-port N           Override the device's default TCP port
              --connect-timeout SEC  Connection timeout in seconds (default 15)
              --device HEXID         (mesh only) target mesh device id, e.g. 0102. Defaults to broadcast (FFFF).
              --delay MS             Delay between shots in a sequence, in milliseconds (default 1000)
              --yes                  Skip the "type FIRE to confirm" safety prompt

            Defaults:
              mesh:   192.168.8.1:7008  (SSID containing "TitanFire")
              single: 192.168.4.1:80    (SSID containing "GT2404")

            Examples:
              firefly-cli mesh list
              firefly-cli mesh manual --device 0102 --port 3
              firefly-cli mesh sequence --device 0102 --ports 1,2,3,4 --delay 1500
              firefly-cli single manual --port 1
              firefly-cli single sequence --ports 1,2,3 --delay 800

            Press Ctrl+C during a sequence to stop after the current shot.
            """);
    }
}
