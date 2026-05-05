using System.Collections.Generic;
using UnityEngine;

public enum MeshComplexity { Low, Mid, High }
public enum RenderingMode  { Baseline, GPUInstancing, StaticBatching }

[DisallowMultipleComponent]
public class BenchmarkSpawner : MonoBehaviour
{
    [Header("Configuration")]
    public int            objectCount    = 1000;
    public MeshComplexity meshComplexity = MeshComplexity.Mid;
    public RenderingMode  renderingMode  = RenderingMode.Baseline;
    public bool           autoSpawnOnStart = true;

    [Header("Materials")]
    public Material baselineMaterial;
    public Material instancedMaterial;

    // Grid spacing between sphere centres (sphere radius = 0.5, so gap = 0.5 units)
    const float Spacing = 1.5f;

    // UV-sphere subdivision table.
    // Tri count = lon * 2 * (lat - 1)
    //   Low  : lat= 8, lon= 8  →  112 tris
    //   Mid  : lat=30, lon=30  → 1740 tris
    //   High : lat=100,lon=100 → 19800 tris
    static readonly int[] Lat = { 8,  30,  100 };
    static readonly int[] Lon = { 8,  30,  100 };

    readonly List<GameObject> _objects = new List<GameObject>();
    Mesh _mesh;

    void Start()
    {
        if (autoSpawnOnStart) Spawn();
    }

    // Called by BenchmarkRunner to drive each configuration.
    public void Spawn()
    {
        Clear();

        int lat = Lat[(int)meshComplexity];
        int lon = Lon[(int)meshComplexity];
        _mesh = BuildUVSphere(lat, lon, radius: 0.5f);
        int trisPerObject = _mesh.triangles.Length / 3;

        Material mat = renderingMode == RenderingMode.GPUInstancing
            ? instancedMaterial
            : baselineMaterial;

        Debug.Log($"[BenchmarkSpawner] mode={renderingMode} complexity={meshComplexity} " +
                  $"N={objectCount} tris/obj={trisPerObject} totalTris={objectCount * (long)trisPerObject}");

        int   cols   = Mathf.CeilToInt(Mathf.Sqrt(objectCount));
        float offset = cols * Spacing * 0.5f;

        for (int i = 0; i < objectCount; i++)
        {
            var go = new GameObject($"Obj_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                (i % cols) * Spacing - offset,
                0f,
                (i / cols) * Spacing - offset);

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial          = mat;
            mr.shadowCastingMode       = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows          = false;

            _objects.Add(go);
        }

        if (renderingMode == RenderingMode.StaticBatching)
        {
            long totalVerts = (long)objectCount * _mesh.vertexCount;
            if (totalVerts > 4_000_000)
                Debug.LogWarning($"[BenchmarkSpawner] StaticBatching: ~{totalVerts:N0} total vertices — expect high memory usage.");
            StaticBatchingUtility.Combine(_objects.ToArray(), gameObject);
            Debug.Log("[BenchmarkSpawner] StaticBatchingUtility.Combine complete.");
        }
    }

    // Called by BenchmarkRunner between configurations.
    public void Clear()
    {
        foreach (var go in _objects)
            if (go) Destroy(go);
        _objects.Clear();
        _mesh = null;
    }

    // Builds a UV sphere with the given subdivision counts and radius.
    // Vertex layout: [0] top pole | rings lat=1..latSegments-1 | [last] bottom pole
    static Mesh BuildUVSphere(int latSegments, int lonSegments, float radius)
    {
        int vertCount = 2 + (latSegments - 1) * lonSegments;
        var verts = new Vector3[vertCount];
        var norms = new Vector3[vertCount];
        var uvs   = new Vector2[vertCount];

        // Top pole
        verts[0] = Vector3.up * radius;
        norms[0] = Vector3.up;
        uvs[0]   = new Vector2(0.5f, 1f);

        // Middle rings
        for (int lat = 1; lat < latSegments; lat++)
        {
            float theta    = Mathf.PI * lat / latSegments;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);
            for (int lon = 0; lon < lonSegments; lon++)
            {
                float phi = 2f * Mathf.PI * lon / lonSegments;
                int   idx = 1 + (lat - 1) * lonSegments + lon;
                var   n   = new Vector3(sinTheta * Mathf.Cos(phi), cosTheta, sinTheta * Mathf.Sin(phi));
                verts[idx] = n * radius;
                norms[idx] = n;
                uvs[idx]   = new Vector2((float)lon / lonSegments, 1f - (float)lat / latSegments);
            }
        }

        // Bottom pole
        verts[vertCount - 1] = Vector3.down * radius;
        norms[vertCount - 1] = Vector3.down;
        uvs[vertCount - 1]   = new Vector2(0.5f, 0f);

        // Triangles
        int triCount = lonSegments * 2 * (latSegments - 1);
        var tris = new int[triCount * 3];
        int t = 0;

        // Top fan
        for (int lon = 0; lon < lonSegments; lon++)
        {
            int next = (lon + 1) % lonSegments;
            tris[t++] = 0;
            tris[t++] = 1 + lon;
            tris[t++] = 1 + next;
        }

        // Middle quad strips
        for (int lat = 0; lat < latSegments - 2; lat++)
        {
            int row     = 1 + lat * lonSegments;
            int nextRow = row + lonSegments;
            for (int lon = 0; lon < lonSegments; lon++)
            {
                int next = (lon + 1) % lonSegments;
                tris[t++] = row + lon;   tris[t++] = nextRow + lon;  tris[t++] = nextRow + next;
                tris[t++] = row + lon;   tris[t++] = nextRow + next; tris[t++] = row + next;
            }
        }

        // Bottom fan
        int pole    = vertCount - 1;
        int lastRow = 1 + (latSegments - 2) * lonSegments;
        for (int lon = 0; lon < lonSegments; lon++)
        {
            int next = (lon + 1) % lonSegments;
            tris[t++] = pole;
            tris[t++] = lastRow + next;
            tris[t++] = lastRow + lon;
        }

        var mesh = new Mesh { name = $"UVSphere_lat{latSegments}_lon{lonSegments}" };
        if (vertCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices  = verts;
        mesh.normals   = norms;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        return mesh;
    }
}
