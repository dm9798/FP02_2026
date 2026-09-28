using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BackgroundKochSnowflake : MonoBehaviour
{
    [Header("Line Appearance")]
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Material lineMaterial;

    [Header("Rendering Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    // Runtime-assigned by the spawner
    private int recursionDepth;
    private float baseRadius;
    private float pulseAmplitude;
    private float pulseSpeed;
    private float rotationSpeed;
    private float lifetime;

    private float age;
    private float pulsePhaseOffset;
    private LineRenderer lineRenderer;
    private Material runtimeFallbackMaterial;

    // Called once by the spawner immediately after Instantiate
    public void Initialize(
        Vector2 center,
        int depth,
        float radius,
        float amplitude,
        float pulseSpeedValue,
        float rotationSpeedValue,
        float lifetimeSeconds)
    {
        transform.position = center;
        recursionDepth = depth;
        baseRadius = radius;
        pulseAmplitude = amplitude;
        pulseSpeed = pulseSpeedValue;
        rotationSpeed = rotationSpeedValue;
        lifetime = lifetimeSeconds;
        pulsePhaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        ConfigureLineRenderer();
        Redraw();
    }

    private void Update()
    {
        age += Time.deltaTime;

        if(age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);

        float pulse = Mathf.Sin((age * pulseSpeed) + pulsePhaseOffset);
        float currentRadius = baseRadius + (pulse * pulseAmplitude);

        Redraw(currentRadius);

        // Fade out over the final portion of its life so despawning isn't an abrup
        float fadeStart = lifetime * 0.8f;

        if(age >= fadeStart)
        {
            float fadeT = Mathf.InverseLerp(lifetime, fadeStart, age);
            Color faded = lineColor;
            faded.a = lineColor.a * fadeT;
            lineRenderer.startColor = faded;
            lineRenderer.endColor = faded;
        }
    }

    private void ConfigureLineRenderer()
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        Material material = GetLineMaterial();

        if(material != null)
        {
            lineRenderer.sharedMaterial = material;
        }
    }

    private void Redraw(float radiusOverride = -1f)
    {
        float radiusToUse = radiusOverride >= 0f ? radiusOverride : baseRadius;

        Vector2[] points2D = KochMath.GenerateSnowflake(
            Vector2.zero,
            radiusToUse,
            recursionDepth
        );

        Vector3[] points3D = new Vector3[points2D.Length];

        for(int i = 0; i < points2D.Length; i++)
        {
            points3D[i] = new Vector3(points2D[i].x, points2D[i].y, 0f);
        }

        lineRenderer.positionCount = points3D.Length;
        lineRenderer.SetPositions(points3D);
    }

    private Material GetLineMaterial()
    {
        if(lineMaterial != null)
        {
            return lineMaterial;
        }

        if(runtimeFallbackMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if(shader != null)
            {
                runtimeFallbackMaterial = new Material(shader);
                runtimeFallbackMaterial.name = "BackgroundKochRuntimeMaterial";
            }
        }

        return runtimeFallbackMaterial;
    }
}