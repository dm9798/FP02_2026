using System;
using System.Collections.Generic;
using UnityEngine;

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

    //
    [SerializeField] private string edgeSortingLayerName = "Default";
    [SerializeField] private int edgeSortingOrder = -1;

    // RoomDirector-facing traversal edge colors. Kept separate from normalEdgeColor/parentEdgeColor
    // RoomDirector needs to repaint ALL traversal edges (parent + 3 children) to the same "sealed" or "open" color at runtime, regardless of
    // their original per-edge default
    [Header("Room Director Traversal Colors")]
    [SerializeField] private Color sealedTraversalColor = Color.red;
    [SerializeField] private Color openTraversalColor = Color.green;

    [Header("Child Edge Collision Toggles")]
    [SerializeField] private bool prevEdgeCollisionEnabled = true;
    [SerializeField] private bool selfEdgeCollisionEnabled = true;
    [SerializeField] private bool nextEdgeCollisionEnabled = true;

    // perimeter blocking edge - player and enemy bounds
    // Depth deliberately kept low for collider performance    
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

    // cached LineRenderer references, mirroring the collider caching above, so
    // RoomDirector can repaint traversal edge colors at runtime
    private LineRenderer prevEdgeLine;
    private LineRenderer selfEdgeLine;
    private LineRenderer nextEdgeLine;
    private LineRenderer parentEdgeLine;

    public Vector2[] ChildEmergeLocalPoints { get; private set; } = new Vector2[3];

    [Header("Enemy Spawning")]
    [Tooltip("The rogues gallery of enemy prefabs this room can spawn from")]
    [SerializeField] private EnemySpawnTable enemySpawnTable;

    //Reference to the player's Transform, passed to the spawned enemy for detection/chase
    [SerializeField] private Transform playerTransform;

    // Cached reference to the spawned enemy, if any future systems query whether this room's enemy is still alive
    //to be deleted - shifted to RoomDirector.cs
    //private EnemyController spawnedEnemy;

    // optional designer-placed marker for where the post-clear key should spawn
    // Falls back to a random walkable point if left unassigned.
    [Header("Room Director Wiring")]   
    [SerializeField] private Transform keySpawnPoint;

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

    // cached closed polygon (local space), built once in GenerateEdges() alongside the
    // perimeter blocking colliders, reusing same cluster points
    private Vector2[] walkablePolygon;

    //public accessor for enemycontroller script to sample points from
    public Vector2[] GetWalkablePolygonLocalPoints()
    {
        return walkablePolygon;
    }

    // For EnemyController's patrol logic to sample random points within the room's bounding radius
    public float BoundaryRadius
    {
        get
        {
            return layoutSettings.BoundaryRadius;
        }
    }

    // public accessor for RoomDirector to know where to spawn the key (within non-child interior of room)
    // RRestrict the key's candidate points to the ParentEdgeLocalStart, ParentEdgeLocalEnd, center triangle
    // AND requiring they also satisfy the existing walkablePolygon check, so a key never spawns outside physical bounds
    public bool TryGetKeySpawnWorldPoint(out Vector2 worldPoint)
    {
        if(keySpawnPoint != null)
        {
            worldPoint = keySpawnPoint.position;
            return true;
        }

        if(TryGetRandomPointInNonChildTriangle(out Vector2 localPoint))
        {
            worldPoint = transform.TransformPoint(localPoint);
            return true;
        }

        // Fallback - if the triangle sampling fails fallback to the old room-wide random point rather than leaving key unspawned
        Debug.LogWarning(
            name + ": could not find a valid key spawn point inside the non-child triangle - " +
            "falling back to a random point anywhere in the room's walkable area.",
            this
        );

        return TryGetRandomWalkableWorldPoint(out worldPoint);
    }

    // Rejection-samples a point inside the triangle formed by the parent edge's two endpoints and the room's center - this is the "Non-Child" interior/area
    // deliberately excluding the three child wedges that surround each fractal spike.
    // Also must pass the existing walkablePolygon check, so a key can never spawn outside the room's actual geometry
    private bool TryGetRandomPointInNonChildTriangle(out Vector2 localPoint, int maxAttempts = 30)
    {
        localPoint = layoutSettings != null ? layoutSettings.center : Vector2.zero;

        Vector2 a = ParentEdgeLocalStart;
        Vector2 b = ParentEdgeLocalEnd;
        Vector2 c = layoutSettings.center;

        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
        float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

        for(int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY)
            );

            bool insideTriangle = IsPointInTriangle(candidate, a, b, c);
            bool insideWalkableArea = walkablePolygon == null || walkablePolygon.Length < 3
                || IsPointInPolygon(candidate, walkablePolygon);

            if(insideTriangle && insideWalkableArea)
            {
                localPoint = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b);
        float d2 = Cross(p, b, c);
        float d3 = Cross(p, c, a);

        bool hasNegative = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPositive = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNegative && hasPositive);
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
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
            //GenerateSolidPerimeterWalls();
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

    // Sets ALL child edges' collision (prev/self/next) to the same value in one call
    // Used by RoomDirector when sealing a room
    // false - freely re-enterable at child motif level) and when opening it
    // true - restore normal blocking
    public void SetAllChildEdgeCollisions(bool enabled)
    {
        if(!hasGeneratedEdges)
        {
            Debug.LogWarning(
                "SetAllChildEdgeCollisions called on " + name +
                " before GenerateEdges() has run - nothing to set yet.",
                this
            );

            return;
        }

        if(prevEdgeCollider != null)
            prevEdgeCollider.enabled = enabled;

        if(selfEdgeCollider != null)
            selfEdgeCollider.enabled = enabled;

        if(nextEdgeCollider != null)
            nextEdgeCollider.enabled = enabled;
    }

    // Repaints all 4 traversal edges (parent + 3 children) to a single color, used by
    // RoomDirector to flip the room between "sealed" (red) and "open" (green) at runtime
    public void SetTraversalEdgeColors(Color color)
    {
        if(prevEdgeLine != null)
        {
            prevEdgeLine.startColor = color;
            prevEdgeLine.endColor = color;
        }

        if(selfEdgeLine != null)
        {
            selfEdgeLine.startColor = color;
            selfEdgeLine.endColor = color;
        }

        if(nextEdgeLine != null)
        {
            nextEdgeLine.startColor = color;
            nextEdgeLine.endColor = color;
        }

        if(parentEdgeLine != null)
        {
            parentEdgeLine.startColor = color;
            parentEdgeLine.endColor = color;
        }
    }

    // convenience wrappers using the two Inspector-configured RoomDirector colors.
    public void PaintTraversalEdgesSealed()
    {
        SetTraversalEdgeColors(sealedTraversalColor);
    }

    public void PaintTraversalEdgesOpen()
    {
        SetTraversalEdgeColors(openTraversalColor);
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
            collisionEnabled: prevEdgeCollisionEnabled,
            createdLine: out prevEdgeLine
        );


        selfEdgeCollider = CreateEdge(
            hexPoints[1], hexPoints[2],
            FractalNode.LetterNames[childLetters[1]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[1],
            collisionEnabled: selfEdgeCollisionEnabled,
            createdLine: out selfEdgeLine
        );


        nextEdgeCollider = CreateEdge(
            hexPoints[2], hexPoints[3],
            FractalNode.LetterNames[childLetters[2]],
            isReturnEdge: false,
            targetLetterIndex: childLetters[2],
            collisionEnabled: nextEdgeCollisionEnabled,
            createdLine: out nextEdgeLine
        );

        //parent edge TRIGGER collider
        parentTriggerEdgeCollider = CreateEdge(
            parentStart, parentEnd,
            "Parent",
            isReturnEdge: true,
            targetLetterIndex: -1,
            createdLine: out parentEdgeLine
        );


        CreatePerimeterBlockingEdges(
            snowflakeHexPoints,
            parentStart,
            parentEnd
        );

        // parent edge NON-TRIGGER physical blocking (enemy always, player toggleable)
        // Separate from parent trigger edge above - not linked by code
        CreateParentEdgeBlockers(parentStart, parentEnd);

        SpawnEnemyIfConfigured();
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

        // collects each cluster's fully-anchored point chain (the same array passed to
        // CreatePerimeterBlockingEdge), so BuildWalkablePolygon() can assemble them into one
        // closed polygon afterward, no need to recalculate
        List<List<Vector2>> perimeterChains = new List<List<Vector2>>();

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

            // NEW
            perimeterChains.Add(perimeterPoints);
        }

        //build the walkable polygon from the exact same chains just used for the
        // colliders, so enemy patrol sampling stays consistent with physical perimeter bounds
        BuildWalkablePolygon(perimeterChains, parentStart, parentEnd);
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

        //UNCOMMENT FOR DEBUGGING TO SEE PERIMETER BLOCKING COLLIDERS
        //LineRenderer line = edgeObject.AddComponent<LineRenderer>();
        //line.useWorldSpace = false;
        //line.positionCount = points.Length;
        //line.SetPositions(Array.ConvertAll(points, p => new Vector3(p.x, p.y, 0f)));
        //line.startWidth = edgeWidth;
        //line.endWidth = edgeWidth;
        //line.startColor = normalEdgeColor;
        //line.endColor = normalEdgeColor;
        ////line.startColor = new Color(0, 0, 1, 1f);
        ////line.endColor = new Color(0, 0, 1, 1f);

        //if(edgeMaterial != null)
        //    line.sharedMaterial = edgeMaterial;

        perimeterBlockingColliders.Add(edgeCollider);

        return edgeCollider;
    }

    // create two separate, always-existing parent-edge NON-TRIGGER blocking colliders:
    // one always blocks for enemies
    // one may block the player via boolean toggle
    // both sit on exact same line as existing parent TRIGGER edge, but are separate colliders 
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
    // Outputs the created LineRenderer via createdLine, so callers can cache it
    // for later color repainting (RoomDirector), mirroring the existing collider caching
    private EdgeCollider2D CreateEdge(
        Vector2 start,
        Vector2 end,
        string edgeName,
        bool isReturnEdge,
        int targetLetterIndex,
        out LineRenderer createdLine,
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

        line.sortingLayerName = edgeSortingLayerName;
        line.sortingOrder = edgeSortingOrder;

        if(edgeMaterial != null)
            line.sharedMaterial = edgeMaterial;

        createdLine = line;

        return edgeCollider;
    }

    // called from CreatePerimeterBlockingEdges(), immediately after building the two
    // perimeter chains, s reuses same exact point data (including anchors).
    // perimeterChains[0] and [1] are expected to share their apex point (both chains meet at the self-spike tip)
    private void BuildWalkablePolygon(List<List<Vector2>> perimeterChains, Vector2 parentStart, Vector2 parentEnd)
    {
        if(perimeterChains.Count != 2)
        {
            Debug.LogWarning(
                name + ": BuildWalkablePolygon expected exactly 2 perimeter chains, got " +
                perimeterChains.Count + " - walkable polygon will not be built for this room. " +
                "Patrol sampling will be unavailable.",
                this
            );

            walkablePolygon = null;
            return;
        }

        List<Vector2> polygon = new List<Vector2>();
        polygon.AddRange(perimeterChains[1]);

        // Skip perimeterChains[0]'s first point if it duplicates chain[1]'s last point (the shared
        // apex), to avoid redundant/zero-length edge in the polygon
        for(int i = 0; i < perimeterChains[0].Count; i++)
        {
            if(i == 0 && polygon.Count > 0 &&
                Vector2.Distance(polygon[polygon.Count - 1], perimeterChains[0][0]) < 0.0001f)
            {
                continue;
            }

            polygon.Add(perimeterChains[0][i]);
        }

        walkablePolygon = polygon.ToArray();
    }

    // Point-in-polygon ray-casting algorithm.
    // Inspired by W. Randolph Franklin's PNPOLY algorithm:
    // https://wrf.ecse.rpi.edu//Research/Short_Notes/pnpoly.html
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

    // rejection-sampling helper: picksa random point within the polygon's bounding box, keeps it only if it's actually inside the polygon
    // retries up to maxAttempts times - maxAttempts=30 gives very low chance of failure
    // Returns false if no valid point was found within maxAttempts -
    // enemycontroller caller to treat that as "stay where you are" - rather than somewhere potentially unsafe/illegal
    public bool TryGetRandomWalkablePoint(out Vector2 localPoint, int maxAttempts = 30)
    {
        localPoint = layoutSettings != null ? layoutSettings.center : Vector2.zero;

        if(walkablePolygon == null || walkablePolygon.Length < 3)
            return false;

        float minX = walkablePolygon[0].x;
        float maxX = walkablePolygon[0].x;
        float minY = walkablePolygon[0].y;
        float maxY = walkablePolygon[0].y;

        for(int i = 1; i < walkablePolygon.Length; i++)
        {
            minX = Mathf.Min(minX, walkablePolygon[i].x);
            maxX = Mathf.Max(maxX, walkablePolygon[i].x);
            minY = Mathf.Min(minY, walkablePolygon[i].y);
            maxY = Mathf.Max(maxY, walkablePolygon[i].y);
        }

        for(int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(minY, maxY)
            );

            if(IsPointInPolygon(candidate, walkablePolygon))
            {
                localPoint = candidate;
                return true;
            }
        }

        Debug.LogWarning(
            name + ": TryGetRandomWalkablePoint failed to find a valid point after " +
            maxAttempts + " attempts - falling back to room center.",
            this
        );

        return false;
    }

    // world-space wrapper, for callers (enemycontroller) as they use world space
    public bool TryGetRandomWalkableWorldPoint(out Vector2 worldPoint, int maxAttempts = 30)
    {
        bool found = TryGetRandomWalkablePoint(out Vector2 localPoint, maxAttempts);
        worldPoint = transform.TransformPoint(localPoint);
        return found;
    }

    // called once from GenerateEdges(), after all the boundary/blocking geometry for this room has been built
    private void SpawnEnemyIfConfigured()
    {
        if(enemySpawnTable == null)
            return;

        GameObject enemyPrefab = enemySpawnTable.GetEnemyPrefab();

        if(enemyPrefab == null)
            return;

        GameObject enemyInstance = Instantiate(enemyPrefab, transform);

        EnemyController enemyController = enemyInstance.GetComponent<EnemyController>();

        if(enemyController == null)
        {
            Debug.LogError(
                name + ": spawned enemy prefab \"" + enemyPrefab.name +
                "\" has no EnemyController component - destroying it.",
                this
            );

            Destroy(enemyInstance);
            return;
        }

        enemyController.Initialize(this, playerTransform);

        // notify RoomDirector (if present on this same GameObject) that an enemy now
        // exists to track, so room-cleared detection works without RoomBoundaryGenerator
        // needing to know anything about room-level state itself.
        RoomDirector director = GetComponent<RoomDirector>();

        if(director != null)
        {
            director.RegisterSpawnedEnemy(enemyController);
        }
    }

    // toggles the PARENT TRIGGER edge's collider (parentTriggerEdgeCollider) - the one carrying
    // RoomZoneTrigger that drives FractalUniverseManager's traversal/zoom logic - separately
    // from parentEdgePlayerBlockerCollider (the physical, non-trigger blocker)
    // Bug fix as SealRoom() was only ever calling SetParentEdgeBlocksPlayer(true),
    // which enables the PHYSICAL blocker collider, but never touched parentTriggerEdgeCollider
    // Since both colliders sit on (approximately) the same line, the trigger kept firing
    // RoomZoneTrigger's OnTriggerEnter2D and initiating a transition regardless of whether the
    // physical blocker was stopping normal movement
    public void SetParentTriggerEdgeEnabled(bool enabled)
    {
        if(parentTriggerEdgeCollider != null)
        {
            parentTriggerEdgeCollider.enabled = enabled;
        }
        else
        {
            Debug.LogWarning(
                "SetParentTriggerEdgeEnabled called on " + name +
                " before its parent trigger edge collider has been created.",
                this
            );
        }
    }
}
