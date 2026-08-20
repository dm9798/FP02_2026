using System;
using UnityEngine;

//[RequireComponent(typeof(RootKochLayoutSettings))]
public class RoomBoundaryGenerator : MonoBehaviour
{
    public enum RoomLetter
    {
        A, B, C, D, E, F
    }

    [Header("Room Identity")]
    [SerializeField] private RoomLetter roomLetter = RoomLetter.A;

    [Header("Zoom Transition Wiring")]
    [SerializeField] private FractalNode ownerNode;

    [Header("Edge Visuals")]
    [SerializeField] private Material edgeMaterial;
    [SerializeField] private float edgeWidth = 0.05f;
    [SerializeField] private Color normalEdgeColor = Color.white;
    [SerializeField] private Color parentEdgeColor = Color.red;

    [Header("Traversal Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    private RoomKochLayoutSettings layoutSettings;

    
    //public Vector2[] ChildAttachLocalPoints { get; private set; } = new Vector2[3];

    public Vector2[] ChildEmergeLocalPoints { get; private set; } = new Vector2[3];


    private void Awake()
    {
        layoutSettings = GetComponent<RoomKochLayoutSettings>();
    }

    private void Start()
    {
        GenerateEdges();
        GenerateChildEmergePoints();
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

        Vector2 offset = point - layoutSettings.center;

        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );

        return layoutSettings.center + rotated;
    }

    private void GenerateEdges()
    {
        int rotationSteps = GetRotationSteps();
        float rotationAngle = rotationSteps * 60f;

        Vector2[] baseHexPoints = new Vector2[4];

        for(int i = 0; i < baseHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseHexPoints[i] = layoutSettings.center + layoutSettings.boundaryRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        // Parent edge points calculated in the unrotated Room A frame.
        float scale = layoutSettings.boundaryRadius * 1.67f;
        float widen = layoutSettings.widenRatio * scale;
        float gap = layoutSettings.gapRatio * scale;

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

       
        //TO BE DELETED - using ChildEmergePoints approach instead
        //ChildAttachLocalPoints[0] = (hexPoints[0] + hexPoints[1]) / 2f;
        //ChildAttachLocalPoints[1] = (hexPoints[1] + hexPoints[2]) / 2f;
        //Debug.Log("ChildAttachLocalPoints[1] =" +ChildAttachLocalPoints[1] + gameObject.name);
        //ChildAttachLocalPoints[2] = (hexPoints[2] + hexPoints[3]) / 2f;

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

    //function to set centre point where child motifs emerge from motif
    //no collision behaviour and NOT related to boundary edge collisions!!!!
    //level 1 and greater
    private void GenerateChildEmergePoints()
    {
        int rotationSteps = GetRotationSteps();
        float rotationAngle = rotationSteps * 60f;

        Vector2[] baseEmergeHexPoints = new Vector2[4];

        for(int i = 0; i < baseEmergeHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseEmergeHexPoints[i] = layoutSettings.center + layoutSettings.childMotifEmergeRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        Vector2[] emergeHexPoints = new Vector2[4];

        for(int i = 0; i < emergeHexPoints.Length; i++)
        {
            emergeHexPoints[i] = RotatePoint(
                baseEmergeHexPoints[i],
                rotationAngle
            );
        }

        ChildEmergeLocalPoints[0] = (emergeHexPoints[0] + emergeHexPoints[1]) / 2f;
        ChildEmergeLocalPoints[1] = (emergeHexPoints[1] + emergeHexPoints[2]) / 2f;
        ChildEmergeLocalPoints[2] = (emergeHexPoints[2] + emergeHexPoints[3]) / 2f;
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