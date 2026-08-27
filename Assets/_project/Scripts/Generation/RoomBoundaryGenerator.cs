using System;
using System.Collections.Generic;
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
    [SerializeField] private bool prevEdgeCollisionEnabled = true;
    [SerializeField] private bool selfEdgeCollisionEnabled = true;
    [SerializeField] private bool nextEdgeCollisionEnabled = true;

    // perimeter blocking edge - player and enemy bounds
    // Deliberately kept low for collider performance    
    [Header("Perimeter Blocking Edge Pattern Detail")]
    [SerializeField] private int blockingEdgeRecursionDepth = 2;  

    // anchor point bug fix (parentStart/parentEnd)
    private const float PerimeterAnchorToleranceRatio = 0.75f;

    // note - two colliders on parent edge - 1 for enemy, 1 for player
    [Header("Parent Edge Physical Blocking")]  
    [SerializeField] private string blockingLayerName = "RoomBlocker";   
    [SerializeField] private bool parentEdgeBlocksPlayer = false;

    [SerializeField] private string playerBlockingLayerName = "PlayerBlocker";

    //cached references to parent-edge blocker colliders
    //so can toggled at runtime without rederiving
    private EdgeCollider2D parentEdgePlayerBlockerCollider;  
    private EdgeCollider2D parentEdgeEnemyBlockerCollider;

    // cached references to every perimeter blocking edge collider created 
    private readonly List<EdgeCollider2D> perimeterBlockingColliders = new List<EdgeCollider2D>();

    // cached reference to the parent TRIGGER edge's own collider
    private EdgeCollider2D parentTriggerEdgeCollider;

    [Header("Traversal Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    private RoomKochLayoutSettings layoutSettings;

    // Guards against generating edges twice - once via Initialize() (instantiated path) and again via Start() 
    private bool hasGeneratedEdges = false;

    // Cached references to the three child edges colliders
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

    // Repply 3 collision toggles to their cached colliders
    // Works after GenerateEdges() has run
    // Intended for FUM.ZoomIntoChildCoroutine
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

    public void RestoreAllEdgeCollisionSettings()
    {
        if(!hasGeneratedEdges)
        {
            Debug.LogWarning(
                "RestoreAllEdgeCollisionSettings called on " + name +
                " before GenerateEdges() has run - nothing to restore yet.",
                this
            );

            return;
        }

        ReapplyChildEdgeCollisionSettings();

        if(parentTriggerEdgeCollider != null)
            parentTriggerEdgeCollider.enabled = true;

        foreach(EdgeCollider2D perimeterCollider in perimeterBlockingColliders)
        {
            if(perimeterCollider != null)
                perimeterCollider.enabled = true;
        }

        if(parentEdgeEnemyBlockerCollider != null)
            parentEdgeEnemyBlockerCollider.enabled = true;

        if(parentEdgePlayerBlockerCollider != null)
            parentEdgePlayerBlockerCollider.enabled = parentEdgeBlocksPlayer;
    }

    //  runtime toggle for the parent edge's player-blocking collider
    public void SetParentEdgeBlocksPlayer(bool blocksPlayer)
    {
        parentEdgeBlocksPlayer = blocksPlayer;

        if(parentEdgePlayerBlockerCollider != null)
        {
            parentEdgePlayerBlockerCollider.enabled = parentEdgeBlocksPlayer;
        }
        else
        {
            Debug.LogWarning(
                "SetParentEdgeBlocksPlayer called on " + name +
                " before its parent-edge player blocker collider has been created.",
                this
            );
        }
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

        // rotated snowflakeRadius-based hex points, matching KochSnowflakeMotifRenderer's
        // own hexPoints exactly (same radius, same rotation)        
        Vector2[] snowflakeHexPoints = new Vector2[4];

        for(int i = 0; i < snowflakeHexPoints.Length; i++)
        {
            snowflakeHexPoints[i] = RotatePoint(baseSnowflakeHexPoints[i], rotationAngle);
        }

        Vector2 parentStart = RotatePoint(parentStartBase, rotationAngle);
        Vector2 parentEnd = RotatePoint(parentEndBase, rotationAngle);

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

        parentTriggerEdgeCollider = CreateEdge(
            parentStart, parentEnd,
            "Parent",
            isReturnEdge: true,
            targetLetterIndex: -1
        );

      
        CreatePerimeterBlockingEdges(
            snowflakeHexPoints,
            parentStart,
            parentEnd
        );

        // parent edge physical blocking (enemy always, player toggleable)
        // Separate from parent trigger edge above - not linked by code
        CreateParentEdgeBlockers(parentStart, parentEnd);
    }

    // builds two perimeter blocking edges (left/prev right/next) until they meet
    // Uses KochMath.GetFilteredSnowflakeClusters() logic   
    private void CreatePerimeterBlockingEdges(
        Vector2[] snowflakeHexPoints,
        Vector2 parentStart,
        Vector2 parentEnd)
    {
        List<List<Vector2>> clusters = KochMath.GetFilteredSnowflakeClusters(
            layoutSettings.center,
            layoutSettings.snowflakeRadius,
            blockingEdgeRecursionDepth,
            snowflakeHexPoints,
            parentStart,
            parentEnd
        );

        // warning if tuning variables produce unusual results - clusters != 2
        if(clusters.Count != 2)
        {
            Debug.LogWarning(
                name + ": expected exactly 2 perimeter blocking clusters (prev/next sides) " +
                "at blockingEdgeRecursionDepth " + blockingEdgeRecursionDepth +
                ", but got " + clusters.Count +
                ". Building a blocking edge for each cluster found anyway.",
                this
            );
        }

        // bug fix to stop anchor not attaching approx to cluster near parentStart & parentEnd    
        float anchorTolerance = layoutSettings.snowflakeRadius * PerimeterAnchorToleranceRatio;

        foreach(List<Vector2> cluster in clusters)
        {
            if(cluster.Count == 0)
                continue;

            Vector2 clusterStart = cluster[0];
            Vector2 clusterEnd = cluster[cluster.Count - 1];

            List<Vector2> perimeterPoints = new List<Vector2>();

            if(TryGetCloserAnchor(clusterStart, parentStart, parentEnd, anchorTolerance, out Vector2 anchorNearStart))
            {
                perimeterPoints.Add(anchorNearStart);
            }

            perimeterPoints.AddRange(cluster);

            if(TryGetCloserAnchor(clusterEnd, parentStart, parentEnd, anchorTolerance, out Vector2 anchorNearEnd))
            {
                perimeterPoints.Add(anchorNearEnd);
            }

            CreatePerimeterBlockingEdge(perimeterPoints.ToArray());
        }
    }

   
    // return true (and output chosen anchor) if the nearer candidate is within tolerance
    private bool TryGetCloserAnchor(
        Vector2 clusterPoint,
        Vector2 parentStart,
        Vector2 parentEnd,
        float tolerance,
        out Vector2 anchor)
    {
        float distToStart = Vector2.Distance(clusterPoint, parentStart);
        float distToEnd = Vector2.Distance(clusterPoint, parentEnd);

        if(distToStart <= distToEnd)
        {
            anchor = parentStart;
            return distToStart <= tolerance;
        }

        anchor = parentEnd;
        return distToEnd <= tolerance;
    }

    // create one solid (non-trigger), always-blocking EdgeCollider2D following the given point chain
    // No RoomZoneTrigger - this is physical blocking only, not a traversal trigger
    private EdgeCollider2D CreatePerimeterBlockingEdge(Vector2[] points)
    {
        GameObject edgeObject = new GameObject("Edge_PerimeterBlock");

        edgeObject.transform.SetParent(transform, worldPositionStays: false);

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
        edgeCollider.points = points;
        edgeCollider.isTrigger = false; // solid - always blocks via normal physics collision

        LineRenderer line = edgeObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.SetPositions(Array.ConvertAll(points, p => new Vector3(p.x, p.y, 0f)));
        line.startWidth = edgeWidth;
        line.endWidth = edgeWidth;
        line.startColor = normalEdgeColor;
        line.endColor = normalEdgeColor;

        if(edgeMaterial != null)
            line.sharedMaterial = edgeMaterial;

        perimeterBlockingColliders.Add(edgeCollider);

        return edgeCollider;
    }

    // create two separate, always-existing parent-edge blocking colliders:
    // one always blocks for enemies
    // one may block the player via boolean toggle
    // both sit on exact same line as  existing parent TRIGGER edge, but are separate colliders 
    private void CreateParentEdgeBlockers(Vector2 parentStart, Vector2 parentEnd)
    {
        GameObject enemyBlockerObject = new GameObject("Edge_ParentBlock_Enemy");
        enemyBlockerObject.transform.SetParent(transform, worldPositionStays: false);

        int blockingLayer = LayerMask.NameToLayer(blockingLayerName);

        if(blockingLayer >= 0)
        {
            enemyBlockerObject.layer = blockingLayer;
        }
        else
        {
            Debug.LogWarning(
                name + ": blockingLayerName \"" + blockingLayerName +
                "\" is not a valid layer - " + enemyBlockerObject.name +
                " will remain on the Default layer.",
                this
            );
        }

        parentEdgeEnemyBlockerCollider = enemyBlockerObject.AddComponent<EdgeCollider2D>();
        parentEdgeEnemyBlockerCollider.points = new[] { parentStart, parentEnd };
        parentEdgeEnemyBlockerCollider.isTrigger = false;
       
        GameObject playerBlockerObject = new GameObject("Edge_ParentBlock_Player");
        playerBlockerObject.transform.SetParent(transform, worldPositionStays: false);

        int playerBlockingLayer = LayerMask.NameToLayer(playerBlockingLayerName);

        if(playerBlockingLayer >= 0)
        {
            playerBlockerObject.layer = playerBlockingLayer;
        }
        else
        {
            Debug.LogWarning(
                name + ": playerBlockingLayerName \"" + playerBlockingLayerName +
                "\" is not a valid layer - " + playerBlockerObject.name +
                " will remain on the Default layer.",
                this
            );
        }

        parentEdgePlayerBlockerCollider = playerBlockerObject.AddComponent<EdgeCollider2D>();
        parentEdgePlayerBlockerCollider.points = new[] { parentStart, parentEnd };
        parentEdgePlayerBlockerCollider.isTrigger = false;
   
        parentEdgePlayerBlockerCollider.enabled = parentEdgeBlocksPlayer;
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

    // includes optional collisionEnabled parameter
    // return created EdgeCollider2D for caching 3 child-edge colliders for later use
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
