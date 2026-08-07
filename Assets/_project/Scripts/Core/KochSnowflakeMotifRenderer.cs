using System;
using System.Collections.Generic;
using UnityEngine;

public class KochSnowflakeMotifRenderer : MonoBehaviour
{
    public enum RoomLetter
    {
        A, B, C, D, E, F
    }

    [Header("Room Identity")]
    [SerializeField] private RoomLetter roomLetter = RoomLetter.A;

    [Header("Hexagon Settings")]
    [SerializeField] private Vector2 center = Vector2.zero;
    [SerializeField] private float radius = 3f;

    [Header("Parent Edge Tuning")]
    [SerializeField] private float gapRatio = 0.15f;
    [SerializeField] private float widenRatio = -0.2f;

    [Header("Snowflake Settings")]
    [Range(0, 6)]
    [SerializeField] private int recursionDepth = 5;

    [SerializeField] private float lineWidth = 0.05f;

    [Header("Generated Motif Objects")]
    [SerializeField] private Transform motifContainer;

    [Header("Line Visuals")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = Color.white;

    [Header("Rendering Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    private Material runtimeFallbackMaterial;

    private void Start()
    {
        Draw();
    }

    [ContextMenu("Redraw")]
    private void Draw()
    {
        EnsureMotifContainer();
        ClearMotifContainer();

        int rotationSteps = GetRotationSteps();
        float rotationAngle = rotationSteps * 60f;

        Vector2[] baseHexPoints = new Vector2[4];

        for(int i = 0; i < baseHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseHexPoints[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        Vector2[] hexPoints = new Vector2[4];

        for(int i = 0; i < hexPoints.Length; i++)
        {
            hexPoints[i] = RotatePoint(
                baseHexPoints[i],
                rotationAngle,
                center
            );
        }

        float gap = gapRatio * radius;
        float widen = widenRatio * radius;

        Vector2 parentStartBase = new Vector2(
            baseHexPoints[3].x - widen,
            baseHexPoints[3].y - gap
        );

        Vector2 parentEndBase = new Vector2(
            baseHexPoints[0].x + widen,
            baseHexPoints[0].y - gap
        );

        Vector2 parentStart = RotatePoint(
            parentStartBase,
            rotationAngle,
            center
        );

        Vector2 parentEnd = RotatePoint(
            parentEndBase,
            rotationAngle,
            center
        );

        List<Vector2> snowflakePoints =
            new List<Vector2>(
                KochMath.GenerateSnowflake(
                    center,
                    radius,
                    recursionDepth
                )
            );

        List<List<Vector2>> clusters =
            new List<List<Vector2>>();

        List<Vector2> currentCluster = null;

        foreach(Vector2 point in snowflakePoints)
        {
            bool outsideAnyRoof =
                IsOutsideEdge(
                    point,
                    hexPoints[0],
                    hexPoints[1],
                    center
                )
                ||
                IsOutsideEdge(
                    point,
                    hexPoints[1],
                    hexPoints[2],
                    center
                )
                ||
                IsOutsideEdge(
                    point,
                    hexPoints[2],
                    hexPoints[3],
                    center
                );

            bool insideParent =
                !IsOutsideEdge(
                    point,
                    parentStart,
                    parentEnd,
                    center
                );

            bool keep = outsideAnyRoof || insideParent;

            if(keep)
            {
                if(currentCluster == null)
                {
                    currentCluster = new List<Vector2>();
                    clusters.Add(currentCluster);
                }

                currentCluster.Add(point);
            }
            else
            {
                currentCluster = null;
            }
        }

        foreach(List<Vector2> cluster in clusters)
        {
            if(cluster.Count >= 2)
            {
                CreateLineSegment(cluster.ToArray());
            }
        }

        CreateLineSegment(
            new[]
            {
                parentStart,
                parentEnd
            }
        );
    }


    // 2 x methods to ensure line renderers on diff scripts same object not destroyed    
    private void EnsureMotifContainer()
    {
        if(motifContainer != null)
            return;

        Transform existingContainer =
            transform.Find("KochMotifLines");

        if(existingContainer != null)
        {
            motifContainer = existingContainer;
            return;
        }

        GameObject container =
            new GameObject("KochMotifLines");

        container.transform.SetParent(
            transform,
            worldPositionStays: false
        );

        motifContainer = container.transform;
    }

    
    private void ClearMotifContainer()
    {
        for(int i = motifContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child =
                motifContainer.GetChild(i).gameObject;

            if(Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void CreateLineSegment(Vector2[] points)
    {
        GameObject segmentObject =
            new GameObject("KochSegment");

        segmentObject.transform.SetParent(
            motifContainer,
            worldPositionStays: false
        );

        LineRenderer line =
            segmentObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = points.Length;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        Material material = GetLineMaterial();

        if(material != null)
        {
            line.sharedMaterial = material;
        }

        Vector3[] points3D =
            new Vector3[points.Length];

        for(int i = 0; i < points.Length; i++)
        {
            points3D[i] = new Vector3(
                points[i].x,
                points[i].y,
                0f
            );
        }

        line.SetPositions(points3D);
    }

    private Material GetLineMaterial()
    {
        if(lineMaterial != null)
            return lineMaterial;

        if(runtimeFallbackMaterial == null)
        {
            Shader shader =
                Shader.Find("Sprites/Default");

            if(shader != null)
            {
                runtimeFallbackMaterial =
                    new Material(shader);

                runtimeFallbackMaterial.name =
                    "KochSnowflakeRuntimeMaterial";
            }
        }

        return runtimeFallbackMaterial;
    }

    private int GetRotationSteps()
    {
        string letter = roomLetter.ToString();

        int index =
            Array.IndexOf(
                FractalNode.LetterNames,
                letter
            );

        if(index < 0)
        {
            Debug.LogError(
                $"Room letter '{letter}' was not found in " +
                "FractalNode.LetterNames.",
                this
            );

            return 0;
        }

        return index;
    }

    private Vector2 RotatePoint(
        Vector2 point,
        float angleDegrees,
        Vector2 pivot)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        Vector2 offset = point - pivot;

        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );

        return pivot + rotated;
    }

    private bool IsOutsideEdge(
        Vector2 point,
        Vector2 edgeStart,
        Vector2 edgeEnd,
        Vector2 referenceCenter)
    {
        Vector2 edgeDirection =
            edgeEnd - edgeStart;

        Vector2 toPoint =
            point - edgeStart;

        Vector2 toCenter =
            referenceCenter - edgeStart;

        float crossPoint =
            edgeDirection.x * toPoint.y
            - edgeDirection.y * toPoint.x;

        float crossCenter =
            edgeDirection.x * toCenter.y
            - edgeDirection.y * toCenter.x;

        return Mathf.Sign(crossPoint)
            != Mathf.Sign(crossCenter);
    }
}