using Firefly.MockDevice;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

string mode = args[0].ToLowerInvariant();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

switch (mode)
{
    case "mesh":
    {
        int port = GetIntOption(args, "--port", 7008);
        int deviceCount = GetIntOption(args, "--devices", 1);
        await new MeshMockServer(port, deviceCount).RunAsync(cts.Token);
        return 0;
    }
    case "single":
    {
        int port = GetIntOption(args, "--port", 80);
        await new SingleMockServer(port).RunAsync(cts.Token);
        return 0;
    }
    default:
        Console.Error.WriteLine($"Unknown mode '{mode}'; expected 'mesh' or 'single'.");
        PrintUsage();
        return 2;
}

static int GetIntOption(string[] args, string name, int defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out int value))
            return value;
    }
    return defaultValue;
}

static void PrintUsage()
{
    Console.WriteLine("""
        firefly-mockdevice -- fake FireFly device for local development/testing,
        speaking either wire protocol over plain TCP on localhost.

        Usage:
          firefly-mockdevice mesh   [--port 7008] [--devices N]
          firefly-mockdevice single [--port 80]

        Point Firefly.Cli (--host 127.0.0.1 --tcp-port <port>) or PyroPilot.App's
        device connection settings at 127.0.0.1 with the matching port instead of
        a real device's WiFi AP. Ctrl+C to stop.
        """);
}
