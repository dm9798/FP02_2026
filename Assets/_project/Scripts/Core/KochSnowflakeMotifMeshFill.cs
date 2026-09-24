
// KochSnowflakeMotifMeshFill - generates a filled Mesh for the same closed outline that KochSnowflakeMotifRenderer draws with LineRenderer segments
// this reconstructs one continuous boundary loop from the same clusters as KochSnowflakeMotifRenderer
// plus the closing parentStart/parentEnd segment) and triangulates it via ear clipping
//
// Since room prefabs have fixed geometry, the mesh is baked ONCE in the Editor via the "Bake Fill Mesh" context
// menu action, saved as a persistent Mesh asset, and simply assigned at runtime - Start()
// does no triangulation work at all in Play mode or in a build.
//
// Must be attached alongside KochSnowflakeMotifRenderer + RoomKochLayoutSettings on the same room prefab

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[RequireComponent(typeof(RoomKochLayoutSettings))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class KochSnowflakeMotifMeshFill : MonoBehaviour
{
    public enum RoomLetter
    {
        A, B, C, D, E, F
    }

    [Header("Room Identity")]
    [SerializeField] private RoomLetter roomLetter = RoomLetter.A;

    [Header("Snowflake Settings")]
    [Range(0, 6)]
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

    private RoomKochLayoutSettings layoutSettings;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material runtimeFallbackMaterial;

    private void Awake()
    {
        layoutSettings = GetComponent<RoomKochLayoutSettings>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        if(bakedMesh == null)
        {
            Debug.LogWarning(
                name + ": KochSnowflakeMotifMeshFill has no bakedMesh assigned - use the " +
                "'Bake Fill Mesh' context menu action in the Editor to generate one. No fill " +
                "will be rendered.",
                this
            );

            return;
        }

        meshFilter.sharedMesh = bakedMesh;
        ApplyMaterial();
    }

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
                runtimeFallbackMaterial.name = "KochSnowflakeFillRuntimeMaterial";
            }
        }

        if(runtimeFallbackMaterial != null)
        {
            runtimeFallbackMaterial.color = fillColor;
        }

        return runtimeFallbackMaterial;
    }

    // ---- Editor-only bake path ----
    // Everything below only compiles in the Editor.
    // This is the exact same BuildBoundaryLoop + EarClipTriangulate logic previously run at runtime Start(),
    // now only ever invoked manually via the context menu, then saved as a persistent Mesh asset on disk
#if UNITY_EDITOR
    [ContextMenu("Bake Fill Mesh")]
    private void BakeFillMesh()
    {
        if(layoutSettings == null)
        {
            layoutSettings = GetComponent<RoomKochLayoutSettings>();
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
                name + ": no RoomKochLayoutSettings found on this GameObject - bake aborted.",
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
        mesh.name = $"KochFill_{roomLetter}_depth{recursionDepth}";

        Vector3[] vertices = new Vector3[boundary.Count];

        for(int i = 0; i < boundary.Count; i++)
        {
            vertices[i] = new Vector3(boundary[i].x, boundary[i].y, 0f);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangleIndices.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        SaveMeshAsset(mesh);

        Debug.Log(
            $"{name}: baked fill mesh with {boundary.Count} boundary points, " +
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
            // Overwrite the existing asset in place so old references (if any) stay valid,
            // rather than accumulating duplicate .asset files on repeated bakes.
            EditorUtility.CopySerialized(mesh, existingAsset);
            AssetDatabase.SaveAssets();
            bakedMesh = existingAsset;
            return;
        }

        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
    }

    private List<Vector2> BuildBoundaryLoop()
    {
        int rotationSteps = GetRotationSteps();
        float rotationAngle = rotationSteps * 60f;

        Vector2[] baseHexPoints = new Vector2[4];

        for(int i = 0; i < baseHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseHexPoints[i] = layoutSettings.center + layoutSettings.snowflakeRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        Vector2[] hexPoints = new Vector2[4];

        for(int i = 0; i < hexPoints.Length; i++)
        {
            hexPoints[i] = RotatePoint(baseHexPoints[i], rotationAngle, layoutSettings.center);
        }

        float gap = layoutSettings.gapRatio * layoutSettings.snowflakeRadius;
        float widen = layoutSettings.widenRatio * layoutSettings.snowflakeRadius;

        Vector2 parentStartBase = new Vector2(
            baseHexPoints[3].x - widen,
            baseHexPoints[3].y - gap
        );

        Vector2 parentEndBase = new Vector2(
            baseHexPoints[0].x + widen,
            baseHexPoints[0].y - gap
        );

        Vector2 parentStart = RotatePoint(parentStartBase, rotationAngle, layoutSettings.center);
        Vector2 parentEnd = RotatePoint(parentEndBase, rotationAngle, layoutSettings.center);

        List<List<Vector2>> clusters = KochMath.GetFilteredSnowflakeClusters(
            layoutSettings.center,
            layoutSettings.snowflakeRadius,
            recursionDepth,
            hexPoints,
            parentStart,
            parentEnd
        );

        List<Vector2> boundary = new List<Vector2>();

        foreach(List<Vector2> cluster in clusters)
        {
            boundary.AddRange(cluster);
        }

        if(boundary.Count == 0 || (boundary[boundary.Count - 1] - parentStart).sqrMagnitude > 0.0001f)
        {
            boundary.Add(parentStart);
        }

        boundary.Add(parentEnd);

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

    private int GetRotationSteps()
    {
        string letter = roomLetter.ToString();
        int index = System.Array.IndexOf(FractalNode.LetterNames, letter);

        if(index < 0)
        {
            Debug.LogError(
                $"Room letter '{letter}' was not found in FractalNode.LetterNames.",
                this
            );

            return 0;
        }

        return index;
    }

    private static Vector2 RotatePoint(Vector2 point, float angleDegrees, Vector2 pivot)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        Vector2 offset = point - pivot;

        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );

        return pivot + rotated;
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