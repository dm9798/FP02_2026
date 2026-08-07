using System.Collections.Generic;
using UnityEngine;

public class KochSnowflakeMotifRenderer : MonoBehaviour
{
    public enum RoomLetter
    {
        A, B, C, D, E, F
    }

    [Header("Room Identity")]
    public RoomLetter roomLetter = RoomLetter.A;

    [Header("Hexagon Settings")]
    public Vector2 center = Vector2.zero;
    public float radius = 3f;

    //[Header("Parent Edge Tuning")]
    //public float gap = 0.75f;
    //public float widen = 0.3f;

    //so parent edge autoscales with radius
    [Header("Parent Edge Tuning (as ratio of radius)")]
    public float gapRatio = 0.15f;   // was gap = 0.75f at radius = 5f
    public float widenRatio = -0.2f; // was widen = -2f at radius = 5f

    [Header("Snowflake Settings")]
    [Range(0, 6)] public int recursionDepth = 5;
    public float lineWidth = 0.05f;

    void Start()
    {
        Draw();
    }

    [ContextMenu("Redraw")]
    void Draw()
    {
        foreach(Transform child in transform)
            Destroy(child.gameObject);

        int steps = GetRotationSteps();
        float rotationAngle = steps * 60f;

        Vector2[] baseHexPoints = new Vector2[4];
        for(int i = 0; i < 4; i++)
        {
            float angle = i * 60f;
            baseHexPoints[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        Vector2[] hexPoints = new Vector2[4];
        for(int i = 0; i < 4; i++)
            hexPoints[i] = RotatePoint(baseHexPoints[i], rotationAngle, center);

        float gap = gapRatio * radius;
        float widen = widenRatio * radius;

        Vector2 parentStartBase = new Vector2(baseHexPoints[3].x - widen, baseHexPoints[3].y - gap);
        Vector2 parentEndBase = new Vector2(baseHexPoints[0].x + widen, baseHexPoints[0].y - gap);
        Vector2 parentStart = RotatePoint(parentStartBase, rotationAngle, center);
        Vector2 parentEnd = RotatePoint(parentEndBase, rotationAngle, center);

        List<Vector2> snowflakePoints = new List<Vector2>(
            KochMath.GenerateSnowflake(center, radius, recursionDepth)
        );

        List<List<Vector2>> clusters = new List<List<Vector2>>();
        List<Vector2> currentCluster = null;

        foreach(var p in snowflakePoints)
        {
            bool outsideAnyRoof =
                IsOutsideEdge(p, hexPoints[0], hexPoints[1], center) ||
                IsOutsideEdge(p, hexPoints[1], hexPoints[2], center) ||
                IsOutsideEdge(p, hexPoints[2], hexPoints[3], center);

            bool insideParent = !IsOutsideEdge(p, parentStart, parentEnd, center);

            bool keep = outsideAnyRoof || insideParent;

            if(keep)
            {
                if(currentCluster == null)
                {
                    currentCluster = new List<Vector2>();
                    clusters.Add(currentCluster);
                }
                currentCluster.Add(p);
            }
            else
            {
                currentCluster = null; // break strip here
            }
        }

        foreach(var cluster in clusters)
        {
            if(cluster.Count >= 2)
                CreateLineSegment(cluster.ToArray());
        }

        CreateLineSegment(new Vector2[] { parentStart, parentEnd });
    }

    void CreateLineSegment(Vector2[] points)
    {
        GameObject segObj = new GameObject("Segment");
        segObj.transform.parent = this.transform;

        LineRenderer line = segObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.material = new Material(Shader.Find("Sprites/Default"));

        Vector3[] points3D = new Vector3[points.Length];
        for(int i = 0; i < points.Length; i++)
            points3D[i] = new Vector3(points[i].x, points[i].y, 0);

        line.positionCount = points3D.Length;
        line.SetPositions(points3D);
    }

    int GetRotationSteps()
    {
        return System.Array.IndexOf(FractalNode.LetterNames, roomLetter.ToString());
    }

    Vector2 RotatePoint(Vector2 point, float angleDegrees, Vector2 pivot)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Vector2 offset = point - pivot;
        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );
        return pivot + rotated;
    }

    // cross-product to test which side of parent edge is point on, keep only if on interior side 
    bool IsOutsideEdge(Vector2 point, Vector2 edgeStart, Vector2 edgeEnd, Vector2 refCenter)
    {
        Vector2 edgeDir = edgeEnd - edgeStart;
        Vector2 toPoint = point - edgeStart;
        Vector2 toCenter = refCenter - edgeStart;

        float crossPoint = edgeDir.x * toPoint.y - edgeDir.y * toPoint.x;
        float crossCenter = edgeDir.x * toCenter.y - edgeDir.y * toCenter.x;

        return Mathf.Sign(crossPoint) != Mathf.Sign(crossCenter);
    }
}