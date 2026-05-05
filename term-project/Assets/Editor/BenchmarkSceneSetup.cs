#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BenchmarkSceneSetup
{
    [MenuItem("Tools/Benchmark/Setup Scene")]
    static void SetupScene()
    {
        // Fresh empty scene
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Directional light — no shadows to keep render cost constant across modes
        var lightGo = new GameObject("Directional Light");
        var light   = lightGo.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1f;
        light.shadows   = LightShadows.None;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Camera directly above the grid looking straight down.
        // Grid for N=10000: cols=100, spacing=1.5 → spans ~±75 units.
        // At height 160, FOV 70 → half-width = 160*tan(35°) ≈ 112 > 75.  ✓
        var camGo = new GameObject("BenchmarkCamera");
        var cam   = camGo.AddComponent<Camera>();
        cam.fieldOfView     = 70f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
        camGo.transform.position = new Vector3(0f, 160f, 0f);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Spawner object
        var spawnerGo = new GameObject("BenchmarkSpawner");
        var spawner   = spawnerGo.AddComponent<BenchmarkSpawner>();

        // Wire up materials if they already exist
        var baseline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Baseline.mat");
        var instanced = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Instanced.mat");
        spawner.baselineMaterial  = baseline;
        spawner.instancedMaterial = instanced;

        if (baseline == null || instanced == null)
            Debug.LogWarning("[BenchmarkSceneSetup] Materials not found at Assets/Materials/ — assign them manually.");

        const string scenePath = "Assets/Scenes/InstancingBenchmark.unity";
        EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
            scenePath);
        AssetDatabase.Refresh();

        Debug.Log($"[BenchmarkSceneSetup] Scene saved to {scenePath}");
    }
}
#endif
