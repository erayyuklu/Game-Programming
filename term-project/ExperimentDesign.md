# Experiment Design — GPU Instancing & Draw Call Optimization

## Technical Challenge
Performance impact of draw calls when rendering large numbers of identical objects, and how
GPU Instancing and Static Batching mitigate the CPU bottleneck.

## Independent Variables

| Variable | Values |
|---|---|
| Object count N | 100, 500, 1000, 5000, 10000 |
| Mesh complexity | Low (~300 tri), Mid (~2k tri), High (~20k tri) |
| Rendering mode | Baseline, GPU Instancing, Static Batching |

**Total configurations: 5 × 3 × 3 = 45**

## Dependent Variables
- FPS (avg + 1% low)
- CPU frame time (ms) — `Time.unscaledDeltaTime * 1000`
- GPU frame time (ms) — `FrameTimingManager.GetLatestTimings`
- Draw call count (SetPass calls + Batches)

## Control Variables
- Fixed camera position and FOV
- Single directional light, no shadows
- Physics disabled
- VSync OFF, `Application.targetFrameRate = -1`
- Resolution: 1920×1080
- Measurement window: 3s warm-up + 30s recording per config

## Pipeline
- **Built-in Render Pipeline**, Forward rendering path
- Unity 2022.3.62f3, Metal backend (Apple Silicon)
- Static Batching enabled, Dynamic Batching disabled (Standalone)

## Mesh Strategy
Procedural sphere meshes generated in code (no external packages):
- Low: 4 subdivisions (~300 tri)
- Mid: 12 subdivisions (~2k tri)
- High: 40 subdivisions (~20k tri)

Exact tri counts logged at runtime in `BenchmarkSpawner.cs`.

## Measurement Schedule
45 configs × 33s ≈ ~25 minutes for one full automated run.
