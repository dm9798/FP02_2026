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
        
    [Header("Child Edge Collision Toggles")]
    [Tooltip("If off, the Prev child edge is still drawn but will not trigger traversal.")]
    [SerializeField] private bool prevEdgeCollisionEnabled = true;

    [Tooltip("If off, the Self child edge is still drawn but will not trigger traversal.")]
    [SerializeField] private bool selfEdgeCollisionEnabled = true;

    [Tooltip("If off, the Next child edge is still drawn but will not trigger traversal.")]
    [SerializeField] private bool nextEdgeCollisionEnabled = true;

    [Header("Traversal Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    private RoomKochLayoutSettings layoutSettings;

    // Guards against generating edges twice - once via Initialize() (instantiated path) and again via Start() 
    private bool hasGeneratedEdges = false;

    // cached refs to 3 child edges' own colliders, caught when GenerateEdges() creates them 
    private EdgeCollider2D prevEdgeCollider;
    private EdgeCollider2D selfEdgeCollider;
    private EdgeCollider2D nextEdgeCollider;

    public Vector2[] ChildEmergeLocalPoints { get; private set; } = new Vector2[3];

    // room's own parent edge endpoints local space
    // exposed for FractalUniverseManager's zoom-in coroutine to query parent edge every frame during room animation
    public Vector2 ParentEdgeLocalStart
    {
        get; private set;
    }
    public Vector2 ParentEdgeLocalEnd
    {
        get; private set;
    }

    // helpers - return parent edge endpoints in WORLD space
    public Vector2 GetParentEdgeWorldStart()
    {
        return transform.TransformPoint(ParentEdgeLocalStart);
    }

    public Vector2 GetParentEdgeWorldEnd()
    {
        return transform.TransformPoint(ParentEdgeLocalEnd);
    }

    private void Awake()
    {
        layoutSettings = GetComponent<RoomKochLayoutSettings>();
    }

    private void Start()
    {
        // if Initialize already ran this frame during runtime path, this doesn't run
        if(!hasGeneratedEdges)
        {
            GenerateEdges();
            GenerateChildEmergePoints();
        }
    }

    // Called by FractalUniverseManager immediately after Instantiate(), before obj's own Start() has run
    // Guarantees FUManager assigned before any RoomZoneTrigger is created avoiding race Awake/Start condition
    public void Initialize(FractalUniverseManager manager)
    {
        universeManager = manager;

        if(hasGeneratedEdges)
            return;

        if(layoutSettings == null)
            layoutSettings = GetComponent<RoomKochLayoutSettings>();

        GenerateEdges();
        GenerateChildEmergePoints();
    }

    // re-applies 3 serialized collision toggles to their respective cached colliders.
    // To be called any time after GenerateEdges() run -  warn and return if called earlier
    // For FUM.ZoomIntoChildCoroutine to force room's edge colliders back after KochRoomVisibility hide/show runs
    public void ReapplyChildEdgeCollisionSettings()
    {
        if(!hasGeneratedEdges)
        {
            Debug.LogWarning(
                "ReapplyChildEdgeCollisionSettings called on " + name +
                " before GenerateEdges() has run - nothing to reapply yet.",
                this
            );

            return;
        }

        if(prevEdgeCollider != null)
            prevEdgeCollider.enabled = prevEdgeCollisionEnabled;

        if(selfEdgeCollider != null)
            selfEdgeCollider.enabled = selfEdgeCollisionEnabled;

        if(nextEdgeCollider != null)
            nextEdgeCollider.enabled = nextEdgeCollisionEnabled;
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
        hasGeneratedEdges = true;

        int rotationSteps = GetRotationSteps();
        float rotationAngle = rotationSteps * 60f;

        // Used for the prev/self/next collision edges aka child edges
        // based on root collision boundary hexagon shape
        Vector2[] baseHexPoints = new Vector2[4];

        for(int i = 0; i < baseHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseHexPoints[i] = layoutSettings.center + layoutSettings.BoundaryRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        // separate hex points based on snowflakeRadius, only for parent edge
        // to match KochSnowflakeMotifRenderer's own parent-edge 
        Vector2[] baseSnowflakeHexPoints = new Vector2[4];

        for(int i = 0; i < baseSnowflakeHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseSnowflakeHexPoints[i] = layoutSettings.center + layoutSettings.snowflakeRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        // hex points to match KochSnowflakeMotifRenderers Draw
        float widen = layoutSettings.widenRatio * layoutSettings.snowflakeRadius;
        float gap = layoutSettings.gapRatio * layoutSettings.snowflakeRadius;

        Vector2 parentStartBase = new Vector2(
            baseSnowflakeHexPoints[0].x + widen,
            baseSnowflakeHexPoints[0].y - gap
        );

        Vector2 parentEndBase = new Vector2(
            baseSnowflakeHexPoints[3].x - widen,
            baseSnowflakeHexPoints[3].y - gap
        );

        Vector2[] hexPoints = new Vector2[4];

        for(int i = 0; i < hexPoints.Length; i++)
        {
            hexPoints[i] = RotatePoint(baseHexPoints[i], rotationAngle);
        }

        Vector2 parentStart = RotatePoint(parentStartBase, rotationAngle);
        Vector2 parentEnd = RotatePoint(parentEndBase, rotationAngle);

        // store local space parent edge endpoints for other components to query later        
        ParentEdgeLocalStart = parentStart;
        ParentEdgeLocalEnd = parentEnd;

        int[] childLetters = FractalNode.GetChildLetters(rotationSteps);
                
        prevEdgeCollider = CreateEdge(
            hexPoints[0], hexPoints[1],
            FractalNode.LetterNames[childLetters[0]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[0],
            collisionEnabled: prevEdgeCollisionEnabled
        );

        selfEdgeCollider = CreateEdge(
            hexPoints[1], hexPoints[2],
            FractalNode.LetterNames[childLetters[1]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[1],
            collisionEnabled: selfEdgeCollisionEnabled
        );

        nextEdgeCollider = CreateEdge(
            hexPoints[2], hexPoints[3],
            FractalNode.LetterNames[childLetters[2]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[2],
            collisionEnabled: nextEdgeCollisionEnabled
        );
               
        CreateEdge(
            parentStart, parentEnd,
            "Parent",
            isReturnEdge: true,
            targetLetterIndex: -1
        );

        //blocking edge - physics        
        CreateBlockingEdge(hexPoints[3], parentEnd);   // left side wall
        CreateBlockingEdge(hexPoints[0], parentStart); // right side wall
    }


    // separate logic from CreateEdge(), this is a solid non-trigger collider
    //prevent player from leaving motif
    private void CreateBlockingEdge(Vector2 start, Vector2 end)
    {
        GameObject edgeObject = new GameObject("Edge_SideBlock");

        edgeObject.transform.SetParent(transform, worldPositionStays: false);

        EdgeCollider2D edgeCollider = edgeObject.AddComponent<EdgeCollider2D>();
        edgeCollider.points = new[] { start, end };
        edgeCollider.isTrigger = false; // solid - blocks movement via normal physics collision, not OnTriggerEnter2D

        LineRenderer line = edgeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = edgeWidth;
        line.endWidth = edgeWidth;
        line.startColor = normalEdgeColor;
        line.endColor = normalEdgeColor;

        if(edgeMaterial != null)
            line.sharedMaterial = edgeMaterial;
    }

    // function to set centre point where child motifs emerge from motif
    // no collision behaviour and NOT related to boundary edge collisions!!!!
    // level 1 and greater
    private void GenerateChildEmergePoints()
    {
        int rotationSteps = GetRotationSteps();
        float rotationAngle = rotationSteps * 60f;

        Vector2[] baseEmergeHexPoints = new Vector2[4];

        for(int i = 0; i < baseEmergeHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseEmergeHexPoints[i] = layoutSettings.center + layoutSettings.ChildMotifEmergeRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        Vector2[] emergeHexPoints = new Vector2[4];

        for(int i = 0; i < emergeHexPoints.Length; i++)
        {
            emergeHexPoints[i] = RotatePoint(baseEmergeHexPoints[i], rotationAngle);
        }

        ChildEmergeLocalPoints[0] = (emergeHexPoints[0] + emergeHexPoints[1]) / 2f;
        ChildEmergeLocalPoints[1] = (emergeHexPoints[1] + emergeHexPoints[2]) / 2f;
        ChildEmergeLocalPoints[2] = (emergeHexPoints[2] + emergeHexPoints[3]) / 2f;
    }

    // optional collisionEnabled parameter (default true)
    // returns the created EdgeCollider2D so GenerateEdges() can cache child-edge colliders for later use
    private EdgeCollider2D CreateEdge(
        Vector2 start,
        Vector2 end,
        string edgeName,
        bool isReturnEdge,
        int targetLetterIndex,
        bool collisionEnabled = true)
    {
        GameObject edgeObject = new GameObject($"Edge_{edgeName}");

        edgeObject.transform.SetParent(transform, worldPositionStays: false);

        EdgeCollider2D edgeCollider = edgeObject.AddComponent<EdgeCollider2D>();

        edgeCollider.points = new[] { start, end };
        edgeCollider.isTrigger = true;

        edgeCollider.enabled = collisionEnabled;

        RoomZoneTrigger trigger = edgeObject.AddComponent<RoomZoneTrigger>();

        trigger.roomName = edgeName;
        trigger.isReturnEdge = isReturnEdge;
        trigger.targetLetterIndex = targetLetterIndex;
        trigger.ownerNode = ownerNode;

        // Guaranteed non-null when GenerateEdges() is reached via Initialize
        // may still be null on the Start() fallback approach if not assigned      
        trigger.universeManager = universeManager;

        LineRenderer line = edgeObject.AddComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = edgeWidth;
        line.endWidth = edgeWidth;
        line.startColor = isReturnEdge ? parentEdgeColor : normalEdgeColor;
        line.endColor = isReturnEdge ? parentEdgeColor : normalEdgeColor;

        if(edgeMaterial != null)
            line.sharedMaterial = edgeMaterial;

        return edgeCollider;
    }
}