
// KochSnowflakeRootMeshFill - generates a filled Mesh for the same closed outline that KochSnowflakeRootRenderer draws with a single looping LineRenderer.
// Unlike the room variant, the root's boundary comes directly from KochMath.GenerateSnowflake() as one continuous closed loop - no cluster reassembly needed
//
// Since the root's geometry is fixed, the mesh is instead baked ONCE in the Editor via the "Bake Fill Mesh" context
// menu action, saved as a persistent Mesh asset, and simply assigned at runtime - Start() does no triangulation work at all in Play mode or in a build
//
// Attach alongside KochSnowflakeRootRenderer + RootKochLayoutSettings on the root visual GameObject

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[RequireComponent(typeof(RootKochLayoutSettings))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class KochSnowflakeRootMeshFill : MonoBehaviour
{
    [Header("Snowflake Settings")]
    [SerializeField] private int recursionDepth = 5;

    [Header("Baked Mesh")]
    [SerializeField] private Mesh bakedMesh;  
    [SerializeField] private string bakedMeshFolder = "Assets/_project/GeneratedMeshes/KochFills";

    [Header("Fill Visuals")]
    [SerializeField] private Material fillMaterial;
    [SerializeField] private Color fillColor = new Color(0.2f, 0.4f, 0.8f, 0.5f);

    [Header("Rendering Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -1;

    private RootKochLayoutSettings layoutSettings;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material runtimeFallbackMaterial;

    private void Awake()
    {
        layoutSettings = GetComponent<RootKochLayoutSettings>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        if(bakedMesh == null)
        {
            Debug.LogWarning(
                name + ": KochSnowflakeRootMeshFill has no bakedMesh assigned - use the " +
                "'Bake Fill Mesh' context menu action in the Editor to generate one. No fill " +
                "will be rendered.",
                this
            );

            return;
        }

        meshFilter.sharedMesh = bakedMesh;
        ApplyMaterial();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if(meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if(meshRenderer != null)
        {
            ApplyMaterial();
        }
    }
#endif

    private void ApplyMaterial()
    {
        Material material = GetFillMaterial();

        if(material != null)
        {
            meshRenderer.sharedMaterial = material;
        }

        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
    }

    private Material GetFillMaterial()
    {
        if(fillMaterial != null)
        {
            fillMaterial.color = fillColor;
            return fillMaterial;
        }

        if(runtimeFallbackMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if(shader != null)
            {
                runtimeFallbackMaterial = new Material(shader);
                runtimeFallbackMaterial.name = "KochSnowflakeRootFillRuntimeMaterial";
            }
        }

        if(runtimeFallbackMaterial != null)
        {
            runtimeFallbackMaterial.color = fillColor;
        }

        return runtimeFallbackMaterial;
    }

    // ---- Editor-only bake path ----
#if UNITY_EDITOR
    [ContextMenu("Bake Fill Mesh")]
    private void BakeFillMesh()
    {
        if(layoutSettings == null)
        {
            layoutSettings = GetComponent<RootKochLayoutSettings>();
        }

        if(meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if(meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if(layoutSettings == null)
        {
            Debug.LogError(
                name + ": no RootKochLayoutSettings found on this GameObject - bake aborted.",
                this
            );

            return;
        }

        List<Vector2> boundary = BuildBoundaryLoop();

        if(boundary.Count < 3)
        {
            Debug.LogWarning(
                name + ": could not assemble a valid boundary loop (fewer than 3 points) - " +
                "bake aborted.",
                this
            );

            return;
        }

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<int> triangleIndices = EarClipTriangulate(boundary);
        stopwatch.Stop();

        if(triangleIndices.Count == 0)
        {
            Debug.LogWarning(
                name + ": ear clipping produced no triangles - the boundary loop may be " +
                "self-intersecting or degenerate. Bake aborted.",
                this
            );

            return;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"KochRootFill_depth{recursionDepth}";

        Vector3[] vertices = new Vector3[boundary.Count];

        for(int i = 0; i < boundary.Count; i++)
        {
            vertices[i] = new Vector3(boundary[i].x, boundary[i].y, 0f);
        }

        mesh.vertices = vertices;

        if(triangleIndices.Count >= 3)
        {
            Vector3 a = vertices[triangleIndices[0]];
            Vector3 b = vertices[triangleIndices[1]];
            Vector3 c = vertices[triangleIndices[2]];
            float signedArea = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);

            if(signedArea > 0)
            {
                for(int i = 0; i < triangleIndices.Count; i += 3)
                {
                    int temp = triangleIndices[i + 1];
                    triangleIndices[i + 1] = triangleIndices[i + 2];
                    triangleIndices[i + 2] = temp;
                }
            }
        }

        mesh.triangles = triangleIndices.ToArray();
        Vector2[] uvs = new Vector2[boundary.Count];

        Bounds tempBounds = new Bounds(vertices[0], Vector3.zero);
        foreach(Vector3 v in vertices)
            tempBounds.Encapsulate(v);

        for(int i = 0; i < boundary.Count; i++)
        {
            uvs[i] = new Vector2(
                (boundary[i].x - tempBounds.min.x) / tempBounds.size.x,
                (boundary[i].y - tempBounds.min.y) / tempBounds.size.y
            );
        }

        mesh.uv = uvs;
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        SaveMeshAsset(mesh);

        Debug.Log(
            $"{name}: baked root fill mesh with {boundary.Count} boundary points, " +
            $"{triangleIndices.Count / 3} triangles in {stopwatch.ElapsedMilliseconds}ms " +
            "(one-time Editor cost - no runtime triangulation).",
            this
        );

        bakedMesh = mesh;
        meshFilter.sharedMesh = bakedMesh;
        ApplyMaterial();

        EditorUtility.SetDirty(this);
    }

    private void SaveMeshAsset(Mesh mesh)
    {
        if(!Directory.Exists(bakedMeshFolder))
        {
            Directory.CreateDirectory(bakedMeshFolder);
            AssetDatabase.Refresh();
        }

        string assetPath = $"{bakedMeshFolder}/{mesh.name}.asset";
        Mesh existingAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

        if(existingAsset != null)
        {
            EditorUtility.CopySerialized(mesh, existingAsset);
            AssetDatabase.SaveAssets();
            bakedMesh = existingAsset;
            return;
        }

        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
    }

    // The root's boundary is already one continuous closed loop straight out of KochMath.GenerateSnowflake()
    // (it self-closes via points.Add(points[0])) - no cluster reassembly needed
    private List<Vector2> BuildBoundaryLoop()
    {
        Vector2[] points = KochMath.GenerateSnowflake(
            layoutSettings.center,
            layoutSettings.snowflakeCircumradius,
            recursionDepth
        );

        List<Vector2> boundary = new List<Vector2>(points);

        // GenerateSnowflake's closing point duplicates the first point (points.Add(points[0]))
        // ear clipping expects a simple polygon ring without a duplicate closing vertex
        if(boundary.Count > 1 &&
            (boundary[boundary.Count - 1] - boundary[0]).sqrMagnitude < 0.0001f)
        {
            boundary.RemoveAt(boundary.Count - 1);
        }

        return boundary;
    }

    private List<int> EarClipTriangulate(List<Vector2> polygon)
    {
        List<Vector2> points = new List<Vector2>(polygon);

        if(SignedArea(points) < 0f)
        {
            points.Reverse();
        }

        List<int> indices = new List<int>();

        for(int i = 0; i < points.Count; i++)
        {
            indices.Add(i);
        }

        List<int> triangles = new List<int>();
        int guard = 0;
        int maxIterations = points.Count * points.Count;

        while(indices.Count > 3 && guard < maxIterations)
        {
            guard++;
            bool earFound = false;
            int n = indices.Count;

            for(int i = 0; i < n; i++)
            {
                int iPrev = indices[(i - 1 + n) % n];
                int iCurr = indices[i];
                int iNext = indices[(i + 1) % n];

                Vector2 a = points[iPrev];
                Vector2 b = points[iCurr];
                Vector2 c = points[iNext];

                if(Cross(a, b, c) <= 0f)
                {
                    continue;
                }

                bool isEar = true;

                foreach(int j in indices)
                {
                    if(j == iPrev || j == iCurr || j == iNext)
                    {
                        continue;
                    }

                    if(PointInTriangle(points[j], a, b, c))
                    {
                        isEar = false;
                        break;
                    }
                }

                if(isEar)
                {
                    triangles.Add(iPrev);
                    triangles.Add(iCurr);
                    triangles.Add(iNext);

                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }

            if(!earFound)
            {
                break;
            }
        }

        if(indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }

        return triangles;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);

        bool hasNegative = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPositive = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNegative && hasPositive);
    }

    private static float SignedArea(List<Vector2> points)
    {
        float area = 0f;
        int n = points.Count;

        for(int i = 0; i < n; i++)
        {
            Vector2 current = points[i];
            Vector2 next = points[(i + 1) % n];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
    }
#endif
}