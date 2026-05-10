# Experiment Design — GPU Instancing & Draw Call Optimization

## Technical Challenge
Performance impact of draw calls when rendering large numbers of identical objects, and how
GPU Instancing and Static Batching mitigate the CPU bottleneck.

## Independent Variables

| Variable | Values |
|---|---|
| Object count N | 100, 500, 1000, 5000, 10000 |
| Mesh complexity | Low (112 tri, 58 verts), Mid (1 740 tri, 872 verts), High (19 800 tri, 9 902 verts) |
| Rendering mode | Baseline, GPU Instancing, Static Batching |

**Total configurations: 5 × 3 × 3 = 45**

## Dependent Variables
- FPS (avg + 1% low)
- Wall-clock frame time (ms) — `Time.unscaledDeltaTime * 1000` (includes GPU sync stalls)
- Main-thread CPU time (ms) — `ProfilerRecorder` on `"Main Thread"` (actual CPU work only)
- Render-thread CPU time (ms) — `ProfilerRecorder` on `"Render Thread"`
- GPU frame time (ms) — `FrameTimingManager.GetLatestTimings`
- Draw call batches — `ProfilerRecorder` on `"Batches Count"`
- Memory — `Total Reserved`, `Gfx Reserved`, `GC Reserved` MB via `ProfilerCategory.Memory`

## Control Variables
- Fixed camera position and FOV
- Single directional light, no shadows
- Physics disabled
- VSync OFF, `Application.targetFrameRate = -1`
- Resolution: 1920×1080
- Measurement window: 3s warm-up (per-frame CaptureFrameTimings to prime Metal driver) + 30s recording per config
- Repeats: 3 per configuration (135 total rows); means/std-devs computed in Python

## Pipeline
- **Built-in Render Pipeline**, Forward rendering path
- Unity 2022.3.62f3, Metal backend (Apple Silicon)
- Static Batching enabled, Dynamic Batching disabled (Standalone)

## Mesh Strategy
Procedural UV-sphere meshes generated in code (no external packages).
Subdivision parameters (lat × lon) and resulting counts:

| Level | lat × lon   | Triangles  | Vertices |
|-------|-------------|-----------|---------|
| Low   | 8 × 8       | 112       | 58      |
| Mid   | 30 × 30     | 1 740     | 872     |
| High  | 100 × 100   | 19 800    | 9 902   |

Formula: `tris = lon × 2 × (lat − 1)`, `verts = 2 + (lat − 1) × lon`.
Exact counts are logged to the Unity Console at spawn time via `BenchmarkSpawner.cs`.

## Measurement Schedule
45 configs × 3 repeats × 33s ≈ ~75 minutes for one full automated run.
