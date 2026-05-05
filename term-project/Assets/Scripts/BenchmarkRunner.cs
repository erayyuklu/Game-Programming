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
    ProfilerRecorder _setPassRec;

    // --- FrameTimingManager buffer ---
    readonly FrameTiming[] _ftBuffer = new FrameTiming[1];

    // --- Result accumulator ---
    struct Row
    {
        public int    N;
        public string complexity, mode;
        public float  fpsAvg, fps1pcLow, cpuMs, gpuMs;
        public long   batches, setPassCalls;
    }

    // -------------------------------------------------------------------------

    void Awake()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount  = 0;

        if (spawner == null)
            spawner = GetComponent<BenchmarkSpawner>();

        spawner.autoSpawnOnStart = false;
    }

    void OnEnable()
    {
        _batchesRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
    }

    void OnDisable()
    {
        _batchesRec.Dispose();
        _setPassRec.Dispose();
    }

    void Start()
    {
        Debug.Log($"[BenchmarkRunner] Recorders — batches valid={_batchesRec.Valid} setPass valid={_setPassRec.Valid}");
        StartCoroutine(RunAll());
    }

    // -------------------------------------------------------------------------

    IEnumerator RunAll()
    {
        var rows  = new List<Row>();
        int total = Modes.Length * Complexities.Length * ObjectCounts.Length;
        int idx   = 0;

        foreach (var mode in Modes)
        foreach (var complexity in Complexities)
        foreach (var n in ObjectCounts)
        {
            idx++;
            Debug.Log($"[BenchmarkRunner] Config {idx}/{total} — N={n} {complexity} {mode}");

            spawner.objectCount    = n;
            spawner.meshComplexity = complexity;
            spawner.renderingMode  = mode;
            spawner.Spawn();
            yield return null; // one frame for spawn to settle

            // Warm-up
            yield return new WaitForSecondsRealtime(warmupSeconds);

            // Measurement
            var fpsSamples     = new List<float>();
            var cpuSamples     = new List<float>();
            var gpuSamples     = new List<double>();
            var batchSamples   = new List<long>();
            var setPassSamples = new List<long>();

            float elapsed = 0f;
            while (elapsed < measureSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;

                fpsSamples.Add(1f / dt);
                cpuSamples.Add(dt * 1000f);

                FrameTimingManager.CaptureFrameTimings();
                uint got = FrameTimingManager.GetLatestTimings(1, _ftBuffer);
                gpuSamples.Add(got > 0 ? _ftBuffer[0].gpuFrameTime : 0.0);

                batchSamples.Add(_batchesRec.Valid ? _batchesRec.LastValue : -1L);
                setPassSamples.Add(_setPassRec.Valid ? _setPassRec.LastValue : -1L);

                yield return null;
            }

            rows.Add(Summarise(n, complexity, mode, fpsSamples, cpuSamples, gpuSamples, batchSamples, setPassSamples));
            spawner.Clear();
            yield return null; // frame for cleanup
        }

        WriteCsv(rows);
        Debug.Log("[BenchmarkRunner] All configurations complete.");
    }

    // -------------------------------------------------------------------------

    static Row Summarise(
        int n, MeshComplexity complexity, RenderingMode mode,
        List<float> fps, List<float> cpu, List<double> gpu,
        List<long> batches, List<long> setPass)
    {
        // Sort ascending so bottom of list = worst frames (lowest FPS, highest CPU ms)
        fps.Sort();
        cpu.Sort();

        int bottom1pc = Mathf.Max(1, fps.Count / 100);

        return new Row
        {
            N           = n,
            complexity  = complexity.ToString(),
            mode        = mode.ToString(),
            fpsAvg      = MeanF(fps),
            fps1pcLow   = MeanF(fps, 0, bottom1pc),
            cpuMs       = MeanF(cpu),
            gpuMs       = (float)MeanD(gpu),
            batches     = MedianL(batches),
            setPassCalls = MedianL(setPass),
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
        sb.AppendLine("N,meshComplexity,renderingMode,fpsAvg,fps1pcLow,cpuMs,gpuMs,batches,setPassCalls");
        foreach (var r in rows)
            sb.AppendLine($"{r.N},{r.complexity},{r.mode}," +
                          $"{r.fpsAvg:F2},{r.fps1pcLow:F2}," +
                          $"{r.cpuMs:F3},{r.gpuMs:F3}," +
                          $"{r.batches},{r.setPassCalls}");

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[BenchmarkRunner] CSV → {path}");
    }
}
