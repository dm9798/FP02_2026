using System.Collections.Generic;
using UnityEngine;

// PartialFractalWallPlacer - generates 1 or more concentric "rings" of the room's OWN Koch
// snowflake boundary curve (same math as KochSnowflakeMotifRenderer/KochMath.GenerateSnowflake),
// at a smaller radius sharing the room's own layoutSettings.center, and renders/colliders
// only random and contiguous ARC of each ring's full closed loop - never the whole shape.
// Because every ring shares the same center and each ring uses a strictly smaller radius than the room's
// own boundary, rings can never overlap each other or poke outside the room by construction, the
// same way a smaller square at a common center never overlaps a larger one 

// Runs once per room, from RoomBoundaryGenerator.GenerateEdges(), after CreateParentEdgeBlockers, before BuildPathGrid).
// RoomPathGrid picks up these colliders via existing Physics2D.OverlapCircle sampling - no pathfinding changes needed

[RequireComponent(typeof(RoomBoundaryGenerator))]
[RequireComponent(typeof(RoomKochLayoutSettings))]
public class PartialFractalWallPlacer : MonoBehaviour
{
    [Header("Ring Count")]
    [Tooltip("Number of concentric partial-fractal rings to make")]
    [SerializeField] private int ringCount = 2;

    [Header("Ring Radius Range")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float minRadiusRatio = 0.15f;
    [Range(0.05f, 0.9f)]
    [SerializeField] private float maxRadiusRatio = 0.65f;
    [SerializeField] private float minRadiusSeparationRatio = 0.1f;

    [Header("Fractal Detail")]
    [Range(1, 5)]
    [SerializeField] private int recursionDepth = 3;

    [Header("Arc Coverage")]
    [Range(0.05f, 1f)]
    [SerializeField] private float minArcCoverage = 0.15f;
    [Range(0.05f, 1f)]
    [SerializeField] private float maxArcCoverage = 0.5f;

    [Header("Visuals")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private float lineWidth = 0.05f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    [Header("Collision")]
    [Tooltip("Layer used for each ring arc's EdgeCollider2D")]
    [SerializeField] private string blockingLayerName = "RoomBlocker";

    [Header("Room Child Edge Gap Size")]
    [SerializeField] private float childEdgeGapWorldSize = 0.4f;

    private RoomKochLayoutSettings layoutSettings;
    private Transform motifContainer;
    private RoomBoundaryGenerator boundaryGenerator;

    // 3 REAL child edges (prev/self/next), pulled from KochLayoutSettings.BoundaryRadius - same
    // lines RoomBoundaryGenerator.GenerateEdges() builds
    private Vector2[][] cachedChildEdges;

    private void Awake()
    {
        layoutSettings = GetComponent<RoomKochLayoutSettings>();
        boundaryGenerator = GetComponent<RoomBoundaryGenerator>();
    }

    // Called by RoomBoundaryGenerator.SpawnStealthWalls() after parent/child edge geometry exists, before BuildPathGrid()
    public void PlacePartialFractalWalls()
    {
        if(layoutSettings == null)
        {
            layoutSettings = GetComponent<RoomKochLayoutSettings>();
        }

        if(layoutSettings == null)
        {
            Debug.LogWarning(
                name + ": PartialFractalWallPlacer has no RoomKochLayoutSettings - skipping partial fractal wall placement.",
                this
            );

            return;
        }

        EnsureMotifContainer();
        ClearMotifContainer();

        cachedChildEdges = BuildChildEdgesAtBoundaryRadius();

        float[] ringRadii = BuildRingRadiuses();

        for(int ringIndex = 0; ringIndex < ringRadii.Length; ringIndex++)
        {
            BuildRing(ringRadii[ringIndex], ringIndex);
        }
    }

    // Picks ringCount radiuses, strictly increasing, each expressed as ratio*snowflakeRadius,
    // with minimum gap between adjacent radiuses so rings stay visually distinct/separate
    private float[] BuildRingRadiuses()
    {
        float snowflakeRadius = layoutSettings.snowflakeRadius;
        float minRadius = minRadiusRatio * snowflakeRadius;
        float maxRadius = maxRadiusRatio * snowflakeRadius;
        float minSeparation = minRadiusSeparationRatio * snowflakeRadius;

        List<float> radiuses = new List<float>();
        int guardAttempts = ringCount * 20;

        while(radiuses.Count < ringCount && guardAttempts > 0)
        {
            guardAttempts--;

            float candidate = Random.Range(minRadius, maxRadius);
            bool tooClose = false;

            foreach(float existing in radiuses)
            {
                if(Mathf.Abs(existing - candidate) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }

            if(!tooClose)
            {
                radiuses.Add(candidate);
            }
        }

        if(radiuses.Count < ringCount)
        {
            Debug.LogWarning(
                name + ": PartialFractalWallPlacer only found " + radiuses.Count + "/" + ringCount +
                " ring radii satisfying minRadiusSeparationRatio - placing fewer rings than configured. " +
                "Consider lowering minRadiusSeparationRatio or ringCount, or widening the min/maxRadiusRatio range.",
                this
            );
        }

        radiuses.Sort();
        return radiuses.ToArray();
    }

    // Generates one ring's full closed-loop snowflake points at the given radius (sharing the room's own center)
    // then slices out one random contiguous arc from that loop, splits that arc wherever it comes within childEdgeGapWorldSize of one of the room's three REAL child
    // edges (cachedChildEdges - fixed at BoundaryRadius, NOT rescaled per ring),
    // and builds visual (LineRenderer) + physical (EdgeCollider2D) representations for each resulting sub-arc
    private void BuildRing(float radius, int ringIndex)
    {
        Vector2[] fullLoopPoints = KochMath.GenerateSnowflake(
            layoutSettings.center,
            radius,
            recursionDepth
        );

        GetRingParentEdge(radius, out Vector2 ringParentStart, out Vector2 ringParentEnd);

        int rawCount = fullLoopPoints.Length - 1; // drop GenerateSnowflake's closing duplicate

        List<int> survivingOriginalIndices = new List<int>();

        for(int i = 0; i < rawCount; i++)
        {
            bool insideParent = !KochMath.IsOutsideEdge(
                fullLoopPoints[i], ringParentStart, ringParentEnd, layoutSettings.center);

            if(insideParent)
            {
                survivingOriginalIndices.Add(i);
            }
        }

        if(survivingOriginalIndices.Count < 4)
        {
            Debug.LogWarning(
                name + ": ring " + ringIndex + " had too few points (" + survivingOriginalIndices.Count +
                ") remaining after parent-edge filtering to slice an arc from - skipping this ring. " +
                "Try increasing recursionDepth or checking widenRatio/gapRatio on RoomKochLayoutSettings.",
                this
            );

            return;
        }

        // Find the single largest circular gap between consecutive surviving original indices 
        // this gap corresponds to the removed parent-edge notch. The chain of survivors starts
        // immediately after this gap and ends immediately before it, this is one true continuous remaining chain.
        int breakAfterListIndex = 0;
        int largestGap = -1;

        for(int i = 0; i < survivingOriginalIndices.Count; i++)
        {
            int current = survivingOriginalIndices[i];
            int next = survivingOriginalIndices[(i + 1) % survivingOriginalIndices.Count];

            int gap = next - current;

            if(gap <= 0)
            {
                gap += rawCount; // wrapped past the end of the original array
            }

            if(gap > largestGap)
            {
                largestGap = gap;
                breakAfterListIndex = i;
            }
        }

        int chainLength = survivingOriginalIndices.Count;
        Vector2[] orderedChain = new Vector2[chainLength];

        for(int i = 0; i < chainLength; i++)
        {
            int listIndex = (breakAfterListIndex + 1 + i) % chainLength;
            int originalIndex = survivingOriginalIndices[listIndex];
            orderedChain[i] = fullLoopPoints[originalIndex];
        }

        // slice the random arc from WITHIN this single ordered chain only
        float arcCoverage = Random.Range(minArcCoverage, maxArcCoverage);
        int arcPointCount = Mathf.Clamp(
            Mathf.RoundToInt(chainLength * arcCoverage),
            2,
            chainLength
        );

        int maxStartIndex = chainLength - arcPointCount;
        int startIndex = Random.Range(0, maxStartIndex + 1);

        Vector2[] arcPoints = new Vector2[arcPointCount];

        for(int i = 0; i < arcPointCount; i++)
        {
            arcPoints[i] = orderedChain[startIndex + i];
        }

        List<Vector2[]> subArcs = SplitArcNearChildEdges(arcPoints);

        for(int subArcIndex = 0; subArcIndex < subArcs.Count; subArcIndex++)
        {
            Vector2[] subArc = subArcs[subArcIndex];

            if(subArc.Length < 2)
            {
                continue;
            }

            string segmentName = ringIndex + "_" + subArcIndex;
            CreateRingVisual(subArc, segmentName);
            CreateRingCollider(subArc, segmentName);
        }
    }

    // Removes every arcPoints entry within childEdgeGapWorldSize (perpendicular/spatial distance,
    // clamped to each child edge's own segment extent of any of the room's three REAL child edges (via cachedChildEdges)
    // group the remaining points into separate continuos sub-arrays wherever a removal broke the chain
    // Returns the list of sub-arcs
    private List<Vector2[]> SplitArcNearChildEdges(Vector2[] arcPoints)
    {
        List<Vector2[]> subArcs = new List<Vector2[]>();
        List<Vector2> currentSubArc = new List<Vector2>();

        for(int i = 0; i < arcPoints.Length; i++)
        {
            bool nearAnyChildEdge = false;

            foreach(Vector2[] childEdge in cachedChildEdges)
            {
                float distance = DistancePointToSegment(arcPoints[i], childEdge[0], childEdge[1]);

                if(distance <= childEdgeGapWorldSize)
                {
                    nearAnyChildEdge = true;
                    break;
                }
            }

            if(nearAnyChildEdge)
            {
                if(currentSubArc.Count > 0)
                {
                    subArcs.Add(currentSubArc.ToArray());
                    currentSubArc = new List<Vector2>();
                }

                continue;
            }

            currentSubArc.Add(arcPoints[i]);
        }

        if(currentSubArc.Count > 0)
        {
            subArcs.Add(currentSubArc.ToArray());
        }

        return subArcs;
    }

    // Builds the three REAL child edges (prev, self, next) exactly once, at layoutSettings
    // actual BoundaryRadius (via boundaryGenerator.BoundaryRadius) - matching EXACTLY the same
    // baseHexPoints construction RoomBoundaryGenerator.GenerateEdges() itself uses
    // child edges are fixed, room-wide lines that never rescale, unlike the parent edge's notch which does scale with snowflakeRadius
    private Vector2[][] BuildChildEdgesAtBoundaryRadius()
    {
        float boundaryRadius = boundaryGenerator.BoundaryRadius;
        float rotationAngle = boundaryGenerator.GetRoomRotationAngleDegrees();

        Vector2[] baseHexPoints = new Vector2[4];

        for(int i = 0; i < baseHexPoints.Length; i++)
        {
            float angle = i * 60f;

            baseHexPoints[i] = layoutSettings.center + boundaryRadius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        Vector2[] hexPoints = new Vector2[4];

        for(int i = 0; i < hexPoints.Length; i++)
        {
            hexPoints[i] = RotateAroundCenter(baseHexPoints[i], rotationAngle);
        }

        return new Vector2[][]
        {
            new[] { hexPoints[0], hexPoints[1] }, // prev
            new[] { hexPoints[1], hexPoints[2] }, // self
            new[] { hexPoints[2], hexPoints[3] }  // next
        };
    }

    // Shortest distance from point p to the line SEGMENT a-b (clamped to the segment's own
    // extent) - closest-point-on-segment projection approach
    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abLengthSquared = ab.sqrMagnitude;

        if(abLengthSquared < 0.0000001f)
        {
            return Vector2.Distance(p, a);
        }

        float t = Vector2.Dot(p - a, ab) / abLengthSquared;
        t = Mathf.Clamp01(t);

        Vector2 closestPoint = a + t * ab;
        return Vector2.Distance(p, closestPoint);
    }

    private void EnsureMotifContainer()
    {
        if(motifContainer != null)
        {
            return;
        }

        Transform existingContainer = transform.Find("PartialFractalWalls");

        if(existingContainer != null)
        {
            motifContainer = existingContainer;
            return;
        }

        GameObject container = new GameObject("PartialFractalWalls");
        container.transform.SetParent(transform, worldPositionStays: false);
        motifContainer = container.transform;
    }

    private void ClearMotifContainer()
    {
        for(int i = motifContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = motifContainer.GetChild(i).gameObject;

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

    private void CreateRingVisual(Vector2[] arcPoints, string segmentName)
    {
        GameObject segmentObject = new GameObject("PartialFractalRing_" + segmentName);
        segmentObject.transform.SetParent(motifContainer, worldPositionStays: false);

        LineRenderer line = segmentObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = arcPoints.Length;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        if(lineMaterial != null)
        {
            line.sharedMaterial = lineMaterial;
        }

        Vector3[] points3D = new Vector3[arcPoints.Length];

        for(int i = 0; i < arcPoints.Length; i++)
        {
            points3D[i] = new Vector3(arcPoints[i].x, arcPoints[i].y, 0f);
        }

        line.SetPositions(points3D);
    }

    // Approximate collider for performance
    // traces the SAME arc point chain but do not match every jagged sub-detail beyond recursionDepth.
    // EdgeCollider2D is non-trigger/physics so it physically blocks player, enemy, and any projectile with a standard Collider2D.
    private void CreateRingCollider(Vector2[] arcPoints, string segmentName)
    {
        GameObject colliderObject = new GameObject("PartialFractalRingCollider_" + segmentName);
        colliderObject.transform.SetParent(motifContainer, worldPositionStays: false);

        int blockingLayer = LayerMask.NameToLayer(blockingLayerName);

        if(blockingLayer >= 0)
        {
            colliderObject.layer = blockingLayer;
        }
        else
        {
            Debug.LogWarning(
                name + ": blockingLayerName \"" + blockingLayerName +
                "\" is not a valid layer - " + colliderObject.name +
                " will remain on the Default layer.",
                this
            );
        }

        EdgeCollider2D edgeCollider = colliderObject.AddComponent<EdgeCollider2D>();
        edgeCollider.points = arcPoints;
        edgeCollider.isTrigger = false;
    }

    // Calculate ring's own parentStart/parentEnd at the given radius, using the exact same widen / gap/ hex-point construction
    // RoomBoundaryGenerator.GenerateEdges() uses for the room's own snowflakeRadius-based parent edge
    private void GetRingParentEdge(float ringRadius, out Vector2 ringParentStart, out Vector2 ringParentEnd)
    {
        float rotationAngle = boundaryGenerator.GetRoomRotationAngleDegrees();

        Vector2 baseHexPoint0 = layoutSettings.center + ringRadius * new Vector2(
            Mathf.Cos(0f * Mathf.Deg2Rad), Mathf.Sin(0f * Mathf.Deg2Rad));

        Vector2 baseHexPoint3 = layoutSettings.center + ringRadius * new Vector2(
            Mathf.Cos(3f * 60f * Mathf.Deg2Rad), Mathf.Sin(3f * 60f * Mathf.Deg2Rad));

        float widen = layoutSettings.widenRatio * ringRadius;
        float gap = layoutSettings.gapRatio * ringRadius;

        Vector2 parentStartBase = new Vector2(baseHexPoint0.x + widen, baseHexPoint0.y - gap);
        Vector2 parentEndBase = new Vector2(baseHexPoint3.x - widen, baseHexPoint3.y - gap);

        ringParentStart = RotateAroundCenter(parentStartBase, rotationAngle);
        ringParentEnd = RotateAroundCenter(parentEndBase, rotationAngle);
    }

    // rotation helper - copied from roomboundarygenerator.cs
    private Vector2 RotateAroundCenter(Vector2 point, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        Vector2 offset = point - layoutSettings.center;
        Vector2 rotated = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
        return layoutSettings.center + rotated;
    }
}