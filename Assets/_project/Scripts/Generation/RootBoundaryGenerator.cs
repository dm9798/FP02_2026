using UnityEngine;

public class RootBoundaryGenerator : MonoBehaviour
{
    [Header("Root Hexagonal Edge Settings")]
    [SerializeField] private Vector2 center = Vector2.zero;
    [SerializeField] private float radius = 3f;

    [Header("Generated Boundary Objects")]
    [SerializeField] private Transform boundaryContainer;

    [Header("Edge Visuals")]
    [SerializeField] private Material edgeMaterial;
    [SerializeField] private float edgeWidth = 0.05f;
    [SerializeField] private Color edgeColor = Color.white;

    [Header("Rendering Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;

    private void Start()
    {
        EnsureBoundaryContainer();
        GenerateEdges();
    }

    private void EnsureBoundaryContainer()
    {
        if(boundaryContainer != null)
            return;

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
        Vector2[] points = new Vector2[6];

        for(int i = 0; i < points.Length; i++)
        {
            float angle = 60f + i * 60f;

            points[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        for(int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];

            string roomName =
                FractalNode.LetterNames[i];

            CreateEdge(
                start,
                end,
                roomName,
                targetLetterIndex: i
            );
        }
    }

    private void CreateEdge(
        Vector2 start,
        Vector2 end,
        string roomName,
        int targetLetterIndex)
    {
        GameObject edgeObject =
            new GameObject($"Edge_{roomName}");

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

        trigger.roomName = roomName;
        trigger.targetLetterIndex = targetLetterIndex;
        trigger.isReturnEdge = false;
        trigger.ownerNode = null;

        LineRenderer line =
            edgeObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        line.startWidth = edgeWidth;
        line.endWidth = edgeWidth;
        line.startColor = edgeColor;
        line.endColor = edgeColor;

        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        if(edgeMaterial != null)
        {
            line.sharedMaterial = edgeMaterial;
        }
    }
}