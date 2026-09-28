using System.Collections.Generic;
using UnityEngine;

// RootBoundaryGenerator - builds the 6 traversal trigger edges (Edge_A..Edge_F, one per child
// room letter) for the depth-0 root motif. Unlike RoomBoundaryGenerator, the root has NO parent
// edge/notch to cut - it is the top of the fractal tree, so its boundary is one single, fully
// closed loop with six equal traversal edges and no "return" edge

[RequireComponent(typeof(RootKochLayoutSettings))]
public class RootBoundaryGenerator : MonoBehaviour
{
    [Header("Generated Boundary Objects")]
    [SerializeField] private Transform boundaryContainer;

    [Header("Edge Visuals")]
    [SerializeField] private Material edgeMaterial;
    [SerializeField] private float edgeWidth = 0.05f;
    [SerializeField] private Color edgeColor = Color.white;

    [Header("Rendering Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    [Header("Traversal Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    // Perimeter blocking - physically stops the player and projectiles from leaving the root's
    // boundary, mirroring RoomBoundaryGenerator's blockingLayerName/blockingEdgeRecursionDepth.
    [Header("Perimeter Blocking")]
    [SerializeField] private string blockingLayerName = "RoomBlocker";

    [SerializeField] private int blockingEdgeRecursionDepth = 2;
    [SerializeField] private float perimeterSnowflakeRadiusRatio = 1.6f;

    [Header("Perimeter Blocking Debug Visual")]
    [SerializeField] private bool showPerimeterBlockDebugLine = true;
    [SerializeField] private Color perimeterBlockDebugColor = Color.magenta;
    [SerializeField] private float perimeterBlockDebugLineWidth = 0.08f;

    // cached closed polygon (local space) - the full perimeter loop
    private Vector2[] walkablePolygon;

    private RootKochLayoutSettings layoutSettings;

    private void Awake()
    {
        layoutSettings =
            GetComponent<RootKochLayoutSettings>();
    }

    private void Start()
    {
        EnsureReferences();
        EnsureBoundaryContainer();
        GenerateEdges();
    }

    private void EnsureReferences()
    {
        if(layoutSettings == null)
        {
            layoutSettings =
                GetComponent<RootKochLayoutSettings>();
        }
    }

    private void EnsureBoundaryContainer()
    {
        if(boundaryContainer != null)
        {
            return;
        }

        Transform existingContainer =
            transform.Find("RootBoundaries");

        if(existingContainer != null)
        {
            boundaryContainer = existingContainer;
            return;
        }

        GameObject container =
            new GameObject("RootBoundaries");

        container.transform.SetParent(
            transform,
            worldPositionStays: false
        );

        boundaryContainer = container.transform;
    }

    private void GenerateEdges()
    {
        if(layoutSettings == null)
        {
            Debug.LogError(
                "RootKochLayoutSettings is missing.",
                this
            );

            return;
        }

        Vector2[] points =
            new Vector2[6];

        for(int i = 0; i < points.Length; i++)
        {
            float angle =
                layoutSettings.boundaryAngleOffset
                + i * 60f;

            points[i] =
                layoutSettings.center
                + layoutSettings.BoundaryRadius
                * new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );
        }

        for(int i = 0; i < points.Length; i++)
        {
            Vector2 start =
                points[i];

            Vector2 end =
                points[(i + 1) % points.Length];

            string roomName =
                FractalNode.LetterNames[i];

            CreateEdge(
                start,
                end,
                roomName,
                i
            );
        }

        
        CreatePerimeterBlockingEdge(points);
    }

    // Builds ONE solid, non-trigger EdgeCollider2D tracing the root's fractal boundary
    private void CreatePerimeterBlockingEdge(Vector2[] hexPoints)
    {
        float snowflakeGenerationRadius = layoutSettings.BoundaryRadius * perimeterSnowflakeRadiusRatio;

        Vector2[] rawSnowflakePoints = KochMath.GenerateSnowflake(
            layoutSettings.center,
            snowflakeGenerationRadius,
            blockingEdgeRecursionDepth
        );

        int rawCount = rawSnowflakePoints.Length - 1; // drop GenerateSnowflake's closing duplicate
        List<Vector2> filteredPoints = new List<Vector2>();

        for(int i = 0; i < rawCount; i++)
        {
            Vector2 point = rawSnowflakePoints[i];

            bool outsideAnyRoof =
                KochMath.IsOutsideEdge(point, hexPoints[0], hexPoints[1], layoutSettings.center)
                || KochMath.IsOutsideEdge(point, hexPoints[1], hexPoints[2], layoutSettings.center)
                || KochMath.IsOutsideEdge(point, hexPoints[2], hexPoints[3], layoutSettings.center)
                || KochMath.IsOutsideEdge(point, hexPoints[3], hexPoints[4], layoutSettings.center)
                || KochMath.IsOutsideEdge(point, hexPoints[4], hexPoints[5], layoutSettings.center)
                || KochMath.IsOutsideEdge(point, hexPoints[5], hexPoints[0], layoutSettings.center);

            if(outsideAnyRoof)
            {
                filteredPoints.Add(point);
            }
        }

        if(filteredPoints.Count < 3)
        {
            Debug.LogWarning(
                name + ": CreatePerimeterBlockingEdge filtered down to only " +
                filteredPoints.Count + " points - the root's fractal boundary may not have " +
                "the intended shape. Check blockingEdgeRecursionDepth and BoundaryRadius.",
                this
            );

            return;
        }

        // close the loop back to the first point
        filteredPoints.Add(filteredPoints[0]);

        Vector2[] finalPoints = filteredPoints.ToArray();
        walkablePolygon = finalPoints;

        GameObject edgeObject = new GameObject("Edge_PerimeterBlock");
        edgeObject.transform.SetParent(boundaryContainer, worldPositionStays: false);
        edgeObject.isStatic = false;

        int blockingLayer = LayerMask.NameToLayer(blockingLayerName);

        if(blockingLayer >= 0)
        {
            edgeObject.layer = blockingLayer;
        }
        else
        {
            Debug.LogWarning(
                name + ": blockingLayerName \"" + blockingLayerName +
                "\" is not a valid layer - " + edgeObject.name +
                " will remain on the Default layer.",
                this
            );
        }

        EdgeCollider2D edgeCollider = edgeObject.AddComponent<EdgeCollider2D>();
        edgeCollider.points = finalPoints;
        edgeCollider.isTrigger = false;

        if(showPerimeterBlockDebugLine)
        {
            LineRenderer debugLine = edgeObject.AddComponent<LineRenderer>();
            debugLine.useWorldSpace = false;
            debugLine.loop = true;
            debugLine.positionCount = finalPoints.Length;

            Vector3[] debugPoints3D = new Vector3[finalPoints.Length];

            for(int i = 0; i < finalPoints.Length; i++)
            {
                debugPoints3D[i] = new Vector3(finalPoints[i].x, finalPoints[i].y, 0f);
            }

            debugLine.SetPositions(debugPoints3D);
            debugLine.startWidth = perimeterBlockDebugLineWidth;
            debugLine.endWidth = perimeterBlockDebugLineWidth;
            debugLine.startColor = perimeterBlockDebugColor;
            debugLine.endColor = perimeterBlockDebugColor;
            debugLine.sortingLayerName = sortingLayerName;
            debugLine.sortingOrder = sortingOrder + 10;

            if(edgeMaterial != null)
            {
                debugLine.sharedMaterial = edgeMaterial;
            }
        }
    }

    public static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;

        for(int i = 0; i < polygon.Length; i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            if((pi.y > point.y) != (pj.y > point.y) &&
                point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    private void CreateEdge(
        Vector2 start,
        Vector2 end,
        string roomName,
        int targetLetterIndex)
    {
        GameObject edgeObject =
            new GameObject(
                "Edge_" + roomName
            );

        edgeObject.transform.SetParent(
            boundaryContainer,
            worldPositionStays: false
        );

        EdgeCollider2D edgeCollider =
            edgeObject.AddComponent<EdgeCollider2D>();

        edgeCollider.points = new[]
        {
            start,
            end
        };

        edgeCollider.isTrigger = true;

        RoomZoneTrigger trigger =
            edgeObject.AddComponent<RoomZoneTrigger>();

        trigger.roomName =
            roomName;

        trigger.targetLetterIndex =
            targetLetterIndex;

        trigger.isReturnEdge =
            false;

        trigger.ownerNode =
            null;

        trigger.universeManager =
            universeManager;

        LineRenderer line =
            edgeObject.AddComponent<LineRenderer>();

        line.useWorldSpace =
            false;

        line.positionCount =
            2;

        line.SetPosition(
            0,
            start
        );

        line.SetPosition(
            1,
            end
        );

        line.startWidth =
            edgeWidth;

        line.endWidth =
            edgeWidth;

        line.startColor =
            edgeColor;

        line.endColor =
            edgeColor;

        line.sortingLayerName =
            sortingLayerName;

        line.sortingOrder =
            sortingOrder;

        if(edgeMaterial != null)
        {
            line.sharedMaterial =
                edgeMaterial;
        }
    }
}