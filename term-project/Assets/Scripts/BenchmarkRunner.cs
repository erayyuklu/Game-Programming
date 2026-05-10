using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

// Runs all 45 configurations automatically and writes a CSV.
// Add this component to the same GameObject as BenchmarkSpawner.
[DefaultExecutionOrder(-10)]
public class BenchmarkRunner : MonoBehaviour
{
    [Header("Timing (seconds)")]
    public float warmupSeconds  = 3f;
    public float measureSeconds = 30f;

    [Header("Repeats")]
    public int repeats = 3;

    [Header("Output")]
    public string outputDir = "BenchmarkResults";

    [Header("References")]
    public BenchmarkSpawner spawner; // auto-found on same GameObject if null

    // --- Experiment grid ---
    static readonly int[]            ObjectCounts = { 100, 500, 1000, 5000, 10000 };
    static readonly MeshComplexity[] Complexities = { MeshComplexity.Low, MeshComplexity.Mid, MeshComplexity.High };
    static readonly RenderingMode[]  Modes        = { RenderingMode.Baseline, RenderingMode.GPUInstancing, RenderingMode.StaticBatching };

    // --- Profiler recorders ---
    ProfilerRecorder _batchesRec;
    ProfilerRecorder _drawCallsRec;
    ProfilerRecorder _trianglesRec;
    ProfilerRecorder _verticesRec;
    ProfilerRecorder _mainThreadRec;
    ProfilerRecorder _renderThreadRec;
    ProfilerRecorder _totalMemRec;
    ProfilerRecorder _gfxMemRec;
    ProfilerRecorder _gcMemRec;

    bool _hasDrawCalls;
    bool _hasTriangles;
    bool _hasVertices;
    bool _hasMainThread;
    bool _hasRenderThread;
    bool _hasTotalMem;
    bool _hasGfxMem;
    bool _hasGcMem;

    // --- FrameTimingManager buffer ---
    readonly FrameTiming[] _ftBuffer = new FrameTiming[1];

    // --- Result row ---
    struct Row
    {
        public int    N, repeat;
        public string complexity, mode;
        public float  fpsAvg, fps1pcLow;
        public float  frameTimeMs, mainThreadMs, renderThreadMs, gpuMs;
        public float  totalReservedMB, gfxReservedMB, gcReservedMB, peakReservedMB;
        public long   batches, drawCalls, triangles, vertices;
    }

    // -------------------------------------------------------------------------

    void Awake()
    {
        if (spawner == null)
            spawner = GetComponent<BenchmarkSpawner>();
    }

    void OnEnable()
    {
        _batchesRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");

        // Probe alternate Render stats — valid flag checked in Start()
        _drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _trianglesRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _verticesRec  = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");

        // CPU thread recorders (nanoseconds; convert to ms via / 1_000_000)
        _mainThreadRec   = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        _renderThreadRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");

        // Memory recorders (bytes; convert to MB via / 1_048_576)
        _totalMemRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
        _gfxMemRec   = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Reserved Memory");
        _gcMemRec    = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
    }

    void OnDisable()
    {
        _batchesRec.Dispose();
        _drawCallsRec.Dispose();
        _trianglesRec.Dispose();
        _verticesRec.Dispose();
        _mainThreadRec.Dispose();
        _renderThreadRec.Dispose();
        _totalMemRec.Dispose();
        _gfxMemRec.Dispose();
        _gcMemRec.Dispose();
    }

    void Start()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount  = 0;
        spawner.autoSpawnOnStart = false;

        _hasDrawCalls    = _drawCallsRec.Valid;
        _hasTriangles    = _trianglesRec.Valid;
        _hasVertices     = _verticesRec.Valid;
        _hasMainThread   = _mainThreadRec.Valid;
        _hasRenderThread = _renderThreadRec.Valid;
        _hasTotalMem     = _totalMemRec.Valid;
        _hasGfxMem       = _gfxMemRec.Valid;
        _hasGcMem        = _gcMemRec.Valid;

        Debug.Log($"[BenchmarkRunner] Recorders — " +
                  $"batches={_batchesRec.Valid} drawCalls={_hasDrawCalls} " +
                  $"triangles={_hasTriangles} vertices={_hasVertices} | " +
                  $"mainThread={_hasMainThread} renderThread={_hasRenderThread} | " +
                  $"totalMem={_hasTotalMem} gfxMem={_hasGfxMem} gcMem={_hasGcMem}");

        StartCoroutine(RunAll());
    }

    // -------------------------------------------------------------------------

    IEnumerator RunAll()
    {
        var rows  = new List<Row>();
        int total = Modes.Length * Complexities.Length * ObjectCounts.Length * repeats;
        int idx   = 0;

        foreach (var mode in Modes)
        foreach (var complexity in Complexities)
        foreach (var n in ObjectCounts)
        for (int r = 1; r <= repeats; r++)
        {
            idx++;
            Debug.Log($"[BenchmarkRunner] Config {idx}/{total} — N={n} {complexity} {mode} repeat={r}");

            spawner.objectCount    = n;
            spawner.meshComplexity = complexity;
            spawner.renderingMode  = mode;
            spawner.Spawn();
            yield return null; // one frame for spawn to settle

            // Warm-up — call CaptureFrameTimings every frame to prime Metal driver
            float warmElapsed = 0f;
            while (warmElapsed < warmupSeconds)
            {
                FrameTimingManager.CaptureFrameTimings();
                warmElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Measurement
            var fpsSamples          = new List<float>();
            var frameTimeSamples    = new List<float>();
            var mainThreadSamples   = new List<float>();
            var renderThreadSamples = new List<float>();
            var gpuSamples          = new List<double>();
            var batchSamples        = new List<long>();
            var drawCallSamples     = new List<long>();
            var triangleSamples     = new List<long>();
            var vertexSamples       = new List<long>();
            var totalMemSamples     = new List<float>();
            var gfxMemSamples       = new List<float>();
            var gcMemSamples        = new List<float>();
            float peakReservedMB    = 0f;

            float elapsed = 0f;
            while (elapsed < measureSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;

                fpsSamples.Add(1f / dt);
                frameTimeSamples.Add(dt * 1000f);

                if (_hasMainThread)
                    mainThreadSamples.Add(_mainThreadRec.LastValue / 1_000_000f);
                if (_hasRenderThread)
                    renderThreadSamples.Add(_renderThreadRec.LastValue / 1_000_000f);

                FrameTimingManager.CaptureFrameTimings();
                uint got = FrameTimingManager.GetLatestTimings(1, _ftBuffer);
                // Only record non-zero GPU timings to avoid biasing the mean
                if (got > 0 && _ftBuffer[0].gpuFrameTime > 0.0)
                    gpuSamples.Add(_ftBuffer[0].gpuFrameTime);

                batchSamples.Add(_batchesRec.Valid ? _batchesRec.LastValue : -1L);
                if (_hasDrawCalls) drawCallSamples.Add(_drawCallsRec.LastValue);
                if (_hasTriangles) triangleSamples.Add(_trianglesRec.LastValue);
                if (_hasVertices)  vertexSamples.Add(_verticesRec.LastValue);

                if (_hasTotalMem)
                {
                    float mb = _totalMemRec.LastValue / 1_048_576f;
                    totalMemSamples.Add(mb);
                    if (mb > peakReservedMB) peakReservedMB = mb;
                }
                if (_hasGfxMem) gfxMemSamples.Add(_gfxMemRec.LastValue / 1_048_576f);
                if (_hasGcMem)  gcMemSamples.Add(_gcMemRec.LastValue / 1_048_576f);

                yield return null;
            }

            rows.Add(Summarise(n, r, complexity, mode,
                fpsSamples, frameTimeSamples, mainThreadSamples, renderThreadSamples,
                gpuSamples, batchSamples, drawCallSamples, triangleSamples, vertexSamples,
                totalMemSamples, gfxMemSamples, gcMemSamples, peakReservedMB));

            spawner.Clear();
            yield return null; // frame for GameObjects to be destroyed

            // Release Static Batching combined mesh memory before next config
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            yield return null;
        }

        WriteCsv(rows);
        Debug.Log("[BenchmarkRunner] All configurations complete.");
    }

    // -------------------------------------------------------------------------

    static Row Summarise(
        int n, int repeat, MeshComplexity complexity, RenderingMode mode,
        List<float> fps, List<float> frameTime,
        List<float> mainThread, List<float> renderThread,
        List<double> gpu,
        List<long> batches, List<long> drawCalls, List<long> triangles, List<long> vertices,
        List<float> totalMem, List<float> gfxMem, List<float> gcMem,
        float peakReservedMB)
    {
        // Sort ascending so the bottom of the list = lowest FPS (worst frames)
        fps.Sort();
        frameTime.Sort();
        int bottom1pc = Mathf.Max(1, fps.Count / 100);

        return new Row
        {
            N              = n,
            repeat         = repeat,
            complexity     = complexity.ToString(),
            mode           = mode.ToString(),
            fpsAvg         = MeanF(fps),
            fps1pcLow      = MeanF(fps, 0, bottom1pc),
            frameTimeMs    = MeanF(frameTime),
            mainThreadMs   = mainThread.Count > 0 ? MeanF(mainThread)   : -1f,
            renderThreadMs = renderThread.Count > 0 ? MeanF(renderThread) : -1f,
            gpuMs          = gpu.Count > 0 ? (float)MeanD(gpu) : -1f,
            totalReservedMB = totalMem.Count > 0 ? MeanF(totalMem) : -1f,
            gfxReservedMB  = gfxMem.Count > 0   ? MeanF(gfxMem)   : -1f,
            gcReservedMB   = gcMem.Count > 0     ? MeanF(gcMem)    : -1f,
            peakReservedMB = peakReservedMB > 0f ? peakReservedMB  : -1f,
            batches        = MedianL(batches),
            drawCalls      = drawCalls.Count > 0 ? MedianL(drawCalls) : -1L,
            triangles      = triangles.Count > 0 ? MedianL(triangles) : -1L,
            vertices       = vertices.Count > 0  ? MedianL(vertices)  : -1L,
        };
    }

    static float MeanF(List<float> list, int from = 0, int count = -1)
    {
        if (count < 0) count = list.Count - from;
        double sum = 0;
        for (int i = from; i < from + count; i++) sum += list[i];
        return (float)(sum / count);
    }

    static double MeanD(List<double> list)
    {
        double sum = 0;
        foreach (var v in list) sum += v;
        return sum / list.Count;
    }

    static long MedianL(List<long> list)
    {
        if (list.Count == 0) return -1;
        var sorted = new List<long>(list);
        sorted.Sort();
        return sorted[sorted.Count / 2];
    }

    // -------------------------------------------------------------------------

    void WriteCsv(List<Row> rows)
    {
        string dir  = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("N,meshComplexity,renderingMode,repeat," +
                      "fpsAvg,fps1pcLow," +
                      "frameTimeMs,mainThreadMs,renderThreadMs,gpuMs," +
                      "totalReservedMB,gfxReservedMB,gcReservedMB,peakReservedMB," +
                      "batches,drawCalls,triangles,vertices");

        foreach (var r in rows)
            sb.AppendLine($"{r.N},{r.complexity},{r.mode},{r.repeat}," +
                          $"{r.fpsAvg:F2},{r.fps1pcLow:F2}," +
                          $"{r.frameTimeMs:F3},{r.mainThreadMs:F3},{r.renderThreadMs:F3},{r.gpuMs:F3}," +
                          $"{r.totalReservedMB:F1},{r.gfxReservedMB:F1},{r.gcReservedMB:F1},{r.peakReservedMB:F1}," +
                          $"{r.batches},{r.drawCalls},{r.triangles},{r.vertices}");

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[BenchmarkRunner] CSV → {path}");
    }
}
