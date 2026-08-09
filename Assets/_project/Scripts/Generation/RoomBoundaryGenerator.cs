using System;
using UnityEngine;

public class RoomBoundaryGenerator : MonoBehaviour
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

    [Header("Zoom Transition Wiring")]
    [SerializeField] private FractalZoomController zoomController;
    [SerializeField] private FractalNode ownerNode;

    [Header("Edge Visuals")]
    [SerializeField] private Material edgeMaterial;
    [SerializeField] private float edgeWidth = 0.05f;
    [SerializeField] private Color normalEdgeColor = Color.white;
    [SerializeField] private Color parentEdgeColor = Color.red;

    [Header("Traversal Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    private void Start()
    {
        
        GenerateEdges();
        //Debug.Log("room boundary starting...");
    }

    private int GetRotationSteps()
    {
        string letter = roomLetter.ToString();
        int index = Array.IndexOf(FractalNode.LetterNames, letter);

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

    private Vector2 RotatePoint(Vector2 point, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        Vector2 offset = point - center;

        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );

        return center + rotated;
    }

    private void GenerateEdges()
    {
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

        // Parent edge points calculated in the unrotated Room A frame.
        float scale = radius * 1.67f;
        float widen = widenRatio * scale;
        float gap = gapRatio * scale;

        Vector2 parentStartBase = new Vector2(
            baseHexPoints[3].x - widen,
            baseHexPoints[3].y - gap
        );

        Vector2 parentEndBase = new Vector2(
            baseHexPoints[0].x + widen,
            baseHexPoints[0].y - gap
        );

        Vector2[] hexPoints = new Vector2[4];

        for(int i = 0; i < hexPoints.Length; i++)
        {
            hexPoints[i] = RotatePoint(
                baseHexPoints[i],
                rotationAngle
            );
        }

        Vector2 parentStart = RotatePoint(
            parentStartBase,
            rotationAngle
        );

        Vector2 parentEnd = RotatePoint(
            parentEndBase,
            rotationAngle
        );

        int[] childLetters =
            FractalNode.GetChildLetters(rotationSteps);

        CreateEdge(
            hexPoints[0],
            hexPoints[1],
            FractalNode.LetterNames[childLetters[0]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[0]
        );

        CreateEdge(
            hexPoints[1],
            hexPoints[2],
            FractalNode.LetterNames[childLetters[1]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[1]
        );

        CreateEdge(
            hexPoints[2],
            hexPoints[3],
            FractalNode.LetterNames[childLetters[2]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[2]
        );

        CreateEdge(
            parentStart,
            parentEnd,
            "Parent",
            isReturnEdge: true,
            targetLetterIndex: -1
        );
    }

    private void CreateEdge(
        Vector2 start,
        Vector2 end,
        string edgeName,
        bool isReturnEdge,
        int targetLetterIndex)
    {
        GameObject edgeObject = new GameObject($"Edge_{edgeName}");

        edgeObject.transform.SetParent(
            transform,
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

        trigger.roomName = edgeName;
        trigger.isReturnEdge = isReturnEdge;
        trigger.targetLetterIndex = targetLetterIndex;
        trigger.ownerNode = ownerNode;
        trigger.universeManager = universeManager;

        trigger.zoomController = zoomController;

        LineRenderer line =
            edgeObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = edgeWidth;
        line.endWidth = edgeWidth;
        line.startColor = isReturnEdge
            ? parentEdgeColor
            : normalEdgeColor;
        line.endColor = isReturnEdge
            ? parentEdgeColor
            : normalEdgeColor;

        if(edgeMaterial != null)
        {
            line.sharedMaterial = edgeMaterial;
        }
    }
}