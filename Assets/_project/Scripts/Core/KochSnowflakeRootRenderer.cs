using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class KochSnowflakeRootRenderer : MonoBehaviour
{
    [Header("Snowflake Settings")]
    public Vector2 center = Vector2.zero;
    public float radius = 5f;
    [Range(0, 6)] public int recursionDepth = 3;

    [Header("Line Settings")]
    public float lineWidth = 0.05f;

    void Start()
    {
        Draw();
    }

    [ContextMenu("Redraw")]
    void Draw()
    {
        LineRenderer line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        if(line.material == null)
            line.material = new Material(Shader.Find("Sprites/Default"));

        Vector2[] points2D = KochMath.GenerateSnowflake(center, radius, recursionDepth);
        Vector3[] points3D = new Vector3[points2D.Length];
        for(int i = 0; i < points2D.Length; i++)
            points3D[i] = new Vector3(points2D[i].x, points2D[i].y, 0);

        line.positionCount = points3D.Length;
        line.SetPositions(points3D);
    }
}