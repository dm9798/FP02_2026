using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(RootKochLayoutSettings))]
public class KochSnowflakeRootRenderer : MonoBehaviour
{
    [Header("Snowflake Settings")]
    [Range(0, 6)]
    [SerializeField] private int recursionDepth = 3;

    [Header("Line Settings")]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private Material lineMaterial;

    [Header("Rendering Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    private LineRenderer lineRenderer;
    private RootKochLayoutSettings layoutSettings;
    private Material runtimeFallbackMaterial;

    private void Awake()
    {
        lineRenderer =
            GetComponent<LineRenderer>();

        layoutSettings =
            GetComponent<RootKochLayoutSettings>();
    }

    private void Start()
    {
        Draw();
    }

    [ContextMenu("Redraw")]
    private void Draw()
    {
        EnsureReferences();

        if(layoutSettings == null)
        {
            Debug.LogError(
                "RootKochLayoutSettings is missing.",
                this
            );

            return;
        }

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        lineRenderer.sortingLayerName =
            sortingLayerName;

        lineRenderer.sortingOrder =
            sortingOrder;

        Material material =
            GetLineMaterial();

        if(material != null)
        {
            lineRenderer.sharedMaterial =
                material;
        }

        Vector2[] points2D =
            KochMath.GenerateSnowflake(
                layoutSettings.center,
                layoutSettings.snowflakeCircumradius,
                recursionDepth
            );

        Vector3[] points3D =
            new Vector3[points2D.Length];

        for(int i = 0; i < points2D.Length; i++)
        {
            points3D[i] = new Vector3(
                points2D[i].x,
                points2D[i].y,
                0f
            );
        }

        lineRenderer.positionCount =
            points3D.Length;

        lineRenderer.SetPositions(points3D);
    }

    private void EnsureReferences()
    {
        if(lineRenderer == null)
        {
            lineRenderer =
                GetComponent<LineRenderer>();
        }

        if(layoutSettings == null)
        {
            layoutSettings =
                GetComponent<RootKochLayoutSettings>();
        }
    }

    private Material GetLineMaterial()
    {
        if(lineMaterial != null)
        {
            return lineMaterial;
        }

        if(runtimeFallbackMaterial == null)
        {
            Shader shader =
                Shader.Find("Sprites/Default");

            if(shader != null)
            {
                runtimeFallbackMaterial =
                    new Material(shader);

                runtimeFallbackMaterial.name =
                    "RootKochRuntimeMaterial";
            }
        }

        return runtimeFallbackMaterial;
    }
}