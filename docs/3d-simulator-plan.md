# 3D Fireworks Simulator Plan

## Product goal

Add a deterministic, timeline-synchronized 3D preview to PyroPilot. The first
release is a show-planning visualization, not a physically certified prediction
of a consumer firework product.

## Design constraints

- Playback, pause, stop, and arbitrary seeking must produce repeatable frames.
- Simulation data stays in `PyroPilot.Core`; graphics APIs stay outside it.
- The live-fire path must not depend on rendering performance or renderer state.
- Existing `.pyroshow` packages must continue to load with sensible defaults.
- The renderer must be replaceable without changing timeline or device logic.
- Simulation uses metres and seconds; projection into pixels belongs to the renderer.

## Milestone 1: deterministic simulation core

Status: implemented.

- Store effect shape, launch/burst timing, velocity, particle count, lifetime,
  gravity, drag, and colors with each firework definition.
- Store position, heading, tilt, and a deterministic seed with each cue.
- Evaluate comet and burst particles from an absolute elapsed time.
- Round-trip the new data through `.pyroshow` packages.

Acceptance:

- Repeated samples at the same time are identical.
- Sampling before launch or after particle lifetime returns no active burst.
- Existing shows deserialize using default effect and placement values.
- Core tests pass without a graphics context.

## Milestone 2: GPU/OpenGL vertical slice

- Host rendering with Avalonia `OpenGlControlBase` and Silk.NET OpenGL bindings.
- Upload simulator snapshots to a streaming GPU vertex buffer.
- Render perspective-projected comet and spark point sprites with additive glow.
- Feed the control absolute `CurrentTimeMs` plus the show's active cues.
- Remove the `PreviewBurstViewModel` event-based placeholder after parity is proven.

Acceptance:

- Play, pause, stop, and seek update the preview correctly.
- Two simultaneous cues render at distinct launch positions.
- A saved show reproduces the same burst geometry after reload.
- Renderer failure cannot enable, disable, delay, or trigger live firing.

## Milestone 3: GPU renderer expansion and benchmarks

- Replace per-frame managed arrays with reusable/persistently mapped buffers.
- Add trails, launch-site geometry, a basic bloom pass, and camera orbit/zoom.
- Add particle-capacity benchmarks and GPU capability/error reporting.

Acceptance on the project's reference development machine:

- Sustain 60 FPS at 50,000 visible particles in a 1280x720 viewport.
- Sustain at least 30 FPS at 150,000 visible particles.
- No per-frame allocations proportional to particle count after warm-up.
- Pause and seek settle on the expected deterministic frame.

## Milestone 4: useful show-planning MVP

- Add Peony, Chrysanthemum, Ring, and Palm presets plus comet/mine/fan variants.
- Add cue controls for position, heading, tilt, and preview preset.
- Add launch-site markers and a resettable camera.
- Add effect colors and visual duration to library editing.
- Add performance quality levels for integrated and discrete GPUs.

Acceptance:

- A user can spatially arrange a multi-position show without editing JSON.
- Common shell and cake choreography is distinguishable in preview.
- A five-minute show can be previewed and scrubbed without accumulating state.

## Later milestones

- Secondary breaks, crackle, strobe, willow trails, smoke, wind, and richer cakes.
- Camera keyframes and optional environment/terrain presets.
- Frame-accurate offline video export.
- Product-specific effect authoring and reusable effect-template files.

## Known risks

- Product fidelity is primarily an effect-authoring and catalog-data problem.
- GPU context behavior must be verified separately on Windows, macOS, and Linux.
- Bloom and smoke can dominate fill rate before raw particle count becomes limiting.
- Video export needs a separate fixed-step/offscreen rendering path.
- Effect duration and physical fuse timing are visualization metadata and must not
  be treated as safety guarantees for live pyrotechnic operation.
