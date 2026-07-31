# Firefly.Client

A C# client for the WiFi protocols used by Titan Fireworks "FireFly" firing
systems, reverse-engineered from the vendor's official Android app. Targets
`net9.0`, no external dependencies beyond the BCL.

## Two protocols, one library

The device family speaks two unrelated wire protocols depending on model/mode,
mirrored here as two independent clients:

| | `FireflyMeshClient` | `FireflySingleClient` |
|---|---|---|
| Device | FireFly Plus / GT2404-A **mesh** (multiple nodes) | Standalone FireFly / GT2404 |
| AP SSID contains | `TitanFire` | `GT2404` |
| Default endpoint | `192.168.8.1:7008` | `192.168.4.1:80` |
| Framing | `55 .. 16` binary, checksummed | `AA ..` / `BB ..` ASCII-hex-friendly binary |
| Addressing | per-device 2-byte mesh ID, or broadcast `0xFFFF` | single device, no addressing |

Connect your host to the device's AP first (this library doesn't manage WiFi
association — see [Hosting](#hosting-this-in-a-blazor-app) below), then:

```csharp
await using var mesh = new FireflyMeshClient(); // defaults to 192.168.8.1:7008
await mesh.ConnectAsync();

var login = await mesh.LoginAsync();
if (!login.IsSuccess) throw new InvalidOperationException("Login failed");

IReadOnlyList<ushort> devices = await mesh.GetMeshListAsync();
foreach (var deviceId in devices)
{
    var info = await mesh.GetDeviceInfoAsync(deviceId);
    Console.WriteLine($"{deviceId:X4}: firmware {info.FirmwareDate}");
}

await mesh.ManualFireAsync(devices[0], port: 3);
```

```csharp
await using var single = new FireflySingleClient(); // defaults to 192.168.4.1:80
await single.ConnectAsync();
await single.LoginAsync();
await single.WriteCueAsync(port: 1, durationMs: 1500);
await single.FireAsync();
```

## What's solid vs. what's inferred

Everything in `Protocol.Mesh.MeshCommandBuilder` and
`Protocol.Single.SingleCommandBuilder` is **unit-tested byte-for-byte against
the literal hex strings hardcoded in the original app** (see
`Firefly.Client.Tests`) — that part isn't guesswork, it's a faithful port.

Two things are best-effort reconstructions, not confirmed against real
hardware (not everything about the app's internals could be fully resolved
from analysis, and the app itself relies on some fragile assumptions like
polling `available()`):

1. **V3 (`FireflySingleClient`) response parsing.** The app always reads a
   fixed 4 bytes for every V3 response, but the only response payloads visible
   as literal constants are 3 bytes (e.g. `BB0101`). `SingleResponse` exposes
   `EchoedCommand` (byte 1) and `IsSuccess` (byte 2 == 0x01) based on the
   observed pattern across all 5 known response constants, plus a raw
   `ExtraByte` (byte 3) whose meaning is unconfirmed — this most likely
   matters for `GetStatusAsync()`, which probably needs more than a 1-byte
   result to describe 15 ports' state. Validate against a real device before
   relying on anything beyond `IsSuccess`.
2. **Mesh `PORT_STATUS` per-port breakdown.** `GetPortStatusAsync` gives you a
   validated, checksummed `MeshResponse` with the raw payload in `.Data`, but
   this library doesn't attempt to decode individual port bits within it —
   the app's own handling of this response is entangled with UI/retry state
   that wasn't worth porting faithfully. `MeshResponseParser` covers the
   fields that *were* cleanly recoverable (device list, battery, firmware
   date).
3. **V3 login's trailing byte.** The app's WiFi login path sends
   `AA 05 01 00 00 00 01 04` — one byte longer than its plain BLE-style login
   command alone produces (that 7-byte form is only what the app's *BLE*
   login path uses), and the frame's own length byte doesn't account for the
   extra byte. `SingleCommandBuilder.Login()` reproduces the 8-byte WiFi form
   since that's what a real device receives, but the trailing byte's purpose
   isn't documented anywhere in the app.

Everything else (login, mesh list, manual fire, auto-fire start/stop, cue/plan
write & start/clear/delete, flash LED, modify SSID/password, device info) is
structurally simple (fixed-format command, `0x80`/`0xC0`-style ack) and should
just work.

One deliberate deviation from the app: the mesh reader here parses the
declared length byte and reads exactly that many bytes, instead of the app's
"read one byte, then drain whatever else is sitting in the socket buffer"
loop. It's equivalent when the device behaves, and considerably more robust
when a response is split across TCP segments.

## Hosting this in a Blazor app

**Blazor WebAssembly cannot use this library as-is.** Browsers don't expose
raw TCP sockets, and `FireflyTcpTransport` is a thin wrapper over
`System.Net.Sockets.TcpClient` — there's no WASM-compatible substitute for
talking to a bare TCP device.

Two hosting models work, both because they run real .NET with socket access:

- **Blazor Server**, running on the same machine (or same network) that's
  joined to the device's WiFi AP. Since only one host can be associated with
  a given SoftAP at a time, this basically means running the server locally
  on the operator's laptop/tablet — e.g. `dotnet run` on the tablet itself,
  browser hits `localhost`.
- **.NET MAUI Blazor Hybrid**, packaged as an installable app on the
  operator's phone/tablet. This is the closer analogue to the original Titan
  Fireworks app's deployment model, and additionally gives you access to
  `Microsoft.Maui.Networking`/platform WiFi APIs if you want the app to
  manage joining the device's AP itself rather than relying on the OS WiFi
  settings screen.

Either way, register the clients as scoped/transient services (they hold a
single TCP connection each, so don't share one instance across concurrent
show operations) and drive them from your Blazor components via DI as usual.

## Project layout

```
Firefly.Client/
  Transport/            TCP socket wrapper (IFireflyTransport + FireflyTcpTransport)
  Protocol/Mesh/         V2 mesh frame codec, command builders, response parsing
  Protocol/Single/        V3 single-device frame codec, command builders
  Models/                Response DTOs
  FireflyMeshClient.cs    High-level V2 API
  FireflySingleClient.cs  High-level V3 API
Firefly.Client.Tests/     xUnit tests, including literal-string pinning tests
Firefly.Cli/              Demo console app (see below)
```

## Firefly.Cli — demo console app

A small console app (`firefly-cli`) exercising both clients: manual fire and
a timed firing sequence, against either protocol.

```
firefly-cli mesh   list
firefly-cli mesh   manual   --device 0102 --port 3
firefly-cli mesh   sequence --device 0102 --ports 1,2,3,4 --delay 1500
firefly-cli single manual   --port 1
firefly-cli single sequence --ports 1,2,3 --delay 800
```

Run it with `dotnet run --project Firefly.Cli -- <args>`, or `--help` for the
full option list. By default it asks you to type `FIRE` before actually
issuing a fire command (pass `--yes` to skip that for scripted use); Ctrl+C
during a sequence stops after the shot currently in flight rather than
hard-killing the process. `--host`/`--tcp-port` override the protocol's
default endpoint, which is handy for pointing it at a mock device during
development.
