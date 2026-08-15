# pyro-pilot

A desktop show-control app for the Titan Fireworks FireFly / FireFly Plus firing systems: connect to a device over WiFi, fire ports manually, and build a multi-track, drag-and-drop firework show timeline synced to music.

## Status

Early but functional. WiFi control (both device generations) and the full show-editing workflow work end to end against a real device or the included mock device. Bluetooth, macOS/Linux packaging, and a 3D show preview are not built yet -- see [Roadmap](#roadmap).

## Solution layout

```
PyroPilot.slnx
src/
  Firefly.Client/       Low-level protocol client for both FireFly wire protocols (WiFi/TCP).
                         No UI dependencies -- pure .NET, reused as-is from the original reverse-engineering effort.
  Firefly.Cli/           Console demo for Firefly.Client (manual fire / sequences from the terminal).
  Firefly.MockDevice/    Fake mesh/single-protocol TCP device for developing and testing without real hardware.
  PyroPilot.Core/        Show domain model (Show/Track/FireCue/AudioClip/FireworkDefinition/PairedDevice)
                         and the .pyroshow file format (Persistence/ShowPackage).
  PyroPilot.App/         The desktop app itself: Avalonia UI, MVVM (CommunityToolkit.Mvvm).
tests/
  Firefly.Client.Tests/  Byte-for-byte pinning tests for the wire protocols.
  PyroPilot.Core.Tests/  Show format round-trip / domain logic tests.
```

`Firefly.Client`'s own README (`src/Firefly.Client/README.md`) documents the two wire protocols in detail, including which parts are confirmed against the original app's behavior vs. best-effort reconstructions.

## Getting started

Requires the .NET 9 SDK.

```
dotnet build PyroPilot.slnx
dotnet test PyroPilot.slnx
dotnet run --project src/PyroPilot.App
```

### Developing without hardware

`Firefly.MockDevice` speaks both wire protocols over localhost:

```
dotnet run --project src/Firefly.MockDevice -- mesh   --port 7008 --devices 2
dotnet run --project src/Firefly.MockDevice -- single --port 80
```

Point the app's device connection form (or `firefly-cli --host 127.0.0.1 --tcp-port <port>`) at `127.0.0.1` with the matching port instead of a real device's WiFi AP.

## Features

- **Manual fire** -- connect to a device (mesh or single-device protocol), see its ports as a grid, arm a port then confirm to fire it (a deliberate two-step interaction, not a single click, given this drives real pyrotechnics).
- **Firework media library** -- a reusable product catalog with an embedded reference image and optional YouTube video URL. Image bytes are stored with the definition, so they remain available in portable show snapshots.
- **Show timeline** -- video-editor-style multi-track canvas: drag a firework from the library onto a Fire track to place a cue, drag to move it, drag its edge to resize it. Multiple tracks let overlapping effects fire together; clips within one track can't overlap (so a track behaves like a single output lane).
- **Audio track** -- import a music file onto an Audio track; it plays back in sync with the timeline during preview.
- **Show preview** -- transport controls (play/pause/stop/scrub/zoom) display the reference image for each active firework cue.
- **Live fire from the timeline** -- an explicit "LIVE FIRE" toggle lets a cue's scheduled time actually trigger `ManualFireAsync` on its assigned device/port during playback, turning the timeline into a real show controller rather than just an editor. Off by default.
- **Save / load shows** -- a show saves as a single portable `.pyroshow` file (a zip containing `show.json` plus copies of any audio it references), so moving or sharing a show doesn't leave audio behind.

## Roadmap

Deliberately out of scope for this first pass, in rough priority order:

1. **Bluetooth LE** -- `Firefly.Client` only implements the WiFi/TCP transport today. The V3 single-device wire format is the same one the original app uses over classic BLE, so adding a `IFireflyTransport` implementation per OS (WinRT Bluetooth on Windows, CoreBluetooth on macOS, BlueZ on Linux) is the natural extension point rather than a rewrite.
2. **macOS / Linux verification** -- Avalonia and the audio/show-format layers are cross-platform by design, but only Windows has been exercised so far. The one real platform-specific piece is `AudioPlaybackService` (NAudio's output backend is Windows-only); everything else should port with just testing.
3. **3D show preview** -- revisit particle simulation and atmospheric rendering after the media-first authoring workflow is established.
4. **Mesh device targeting** -- manual fire currently addresses a mesh network's broadcast address; picking a specific mesh device ID (via `GetMeshListAsync`) for per-device control is unwired in the UI.
5. **Installer signing** -- release tags automatically produce an MSI, but it is not yet code-signed. A trusted signing certificate is needed to avoid Windows SmartScreen warnings.

## Windows releases

Publishing a GitHub release with a tag such as `v1.2.3` runs the
`Build Windows installer` workflow. It tests the solution, publishes a self-contained
64-bit Windows build, packages it as an MSI, and attaches the MSI to the release.
Users do not need to install .NET separately.

Release tags must contain a three-part numeric version (`v1.2.3` or `1.2.3`). The
workflow can also be run manually from the Actions tab; manual builds are retained as
workflow artifacts but are not attached to a release.
