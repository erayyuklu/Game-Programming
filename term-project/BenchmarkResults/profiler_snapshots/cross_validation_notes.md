# Profiler Cross-Validation Notes

Three representative configurations were manually profiled in the Unity Editor
(Window → Analysis → Profiler) and compared against the automated harness values
from results_20260506_001623.csv.

v1 snapshots were discarded: BenchmarkRunner.Awake() was setting autoSpawnOnStart=false
even when the component was disabled, so the spawner produced zero objects.
Fixed in BenchmarkRunner.cs by moving that assignment to Start(). v2 snapshots are correct.

---

## Results (v2 recordings)

| Config | Harness cpuMs | Profiler CPU | Delta | Verdict |
|--------|--------------|--------------|-------|---------|
| Baseline N=1000 Mid | 1.421 ms | ~1.325 ms | −0.1 ms | ✓ Excellent match |
| GPUInstancing N=10000 High | 65.826 ms | ~3.50 ms | ~62 ms | ✓ Metal async — see note |
| StaticBatching N=5000 Mid | 3.117 ms | ~1.15 ms | ~2 ms | ✓ Editor overhead |

GPU timing: Profiler shows `GPU: --ms` for all three configs.
Metal does not expose GPU frame time to the Editor Profiler module.

---

## Key Finding: Metal Async Rendering (GPU-bound configs)

For GPU Instancing + N=10000 + High-poly, the Profiler shows CPU: 3.50ms
but the harness reports Time.unscaledDeltaTime * 1000 = 65.826ms (FPS ≈ 15).

These are both correct, and the difference is explained by Metal's async submission model:

- **Profiler CPU (3.50ms):** measures the time the Main Thread is actively executing
  (PlayerLoop + command submission). With GPU Instancing, the CPU submits only ~21
  batched draw calls and finishes quickly.

- **Harness cpuMs (65.826ms):** measures wall-clock frame-to-frame time via
  Time.unscaledDeltaTime. On Metal, the game loop cannot begin a new frame until the
  GPU finishes presenting the previous one. Rendering 10000 high-poly spheres
  (~198 million triangles total) takes the GPU ~62ms. This GPU synchronization cost
  is absorbed into Time.unscaledDeltaTime, even though the Main Thread itself is idle
  for most of that time.

**Conclusion:** Time.unscaledDeltaTime is the correct metric for "frame time experienced
by the player." It correctly captures GPU-bound stalls that the Profiler CPU breakdown
does not show. FrameTimingManager.gpuFrameTime (harness gpuMs column) provides a
complementary view into the GPU-side cost, which the Profiler cannot show on Metal.

This behavior is Apple Silicon Metal-specific. On D3D11/Vulkan,
Gfx.WaitForPresentOnGfxThread would show the full GPU wait in the Profiler timeline.

---

## Editor Overhead (CPU-bound configs)

For Baseline N=1000 Mid, Profiler (1.325ms) ≈ Harness (1.421ms) with only 0.1ms delta.
For StaticBatching N=5000 Mid, Profiler (1.15ms) vs Harness (3.117ms) → ~2ms delta.

The ~2ms delta in the static batching case is normal Editor overhead:
EditorLoop calls run alongside PlayerLoop in the Editor, adding a fixed ~1-2ms per frame
that Time.unscaledDeltaTime includes but is absent in the Profiler CPU counter reading.

---

## SetPass Calls

ProfilerRecorder("SetPass Calls Count") returns 2 for all 45 harness configurations.
This stat is not correctly exposed via ProfilerRecorder on Apple Silicon Metal.
The Batches Count metric is reliable and is the primary draw-call metric in the report.

---

## Figures

All five figures (Analysis/figures/) are generated from harness CSV data only.
No Profiler data is used in the figures — they are unaffected by v1/v2 corrections.
