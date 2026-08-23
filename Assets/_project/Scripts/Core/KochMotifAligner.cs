using UnityEngine;

// SSOT for where does neighbour/child letter X relative to koch snowflake and how big should it be
// does NOT render anything or move any GameObject by itself
// pure calculator: given a letter index returns canonical local position, local rotation, and TARGET RADIUS
// for that neighbour/child, in the local space of whichever transform this component is attached to

public class KochMotifAligner : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool isRootMode = false;

    private RootKochLayoutSettings rootLayoutSettings;
    private RoomKochLayoutSettings roomLayoutSettings;

    [SerializeField] private Vector2 center = Vector2.zero;

    [Header("Root Mode Settings")]
    [SerializeField] private float rootChildOrbitRadiusRatio = 2f / 3f;
    [Tooltip("Only used when isRootMode is true")]
    [SerializeField] private float rootChildTargetRadiusRatio = 1f / 3f;

    [Header("Room Mode Settings")]
    [SerializeField] private int roomLetterIndex = 0;
    [SerializeField] private float roomChildTargetRadiusRatio = 1f / 3f;
    [SerializeField] private float extraRotationOffsetDegrees = 0f;

    private void Awake()
    {
        if(isRootMode)
        {
            rootLayoutSettings = GetComponentInChildren<RootKochLayoutSettings>(true);

            if(rootLayoutSettings == null)
            {
                Debug.LogError(
                    "KochMotifAligner (root mode): no RootKochLayoutSettings found on " +
                    gameObject.name + " (searched self and children) - OwnRadius will be invalid.",
                    this
                );
            }
        }
        else
        {
            roomLayoutSettings = GetComponentInChildren<RoomKochLayoutSettings>(true);

            if(roomLayoutSettings == null)
            {
                Debug.LogError(
                    "KochMotifAligner (room mode): no RoomKochLayoutSettings found on " +
                    gameObject.name + " (searched self and children) - OwnRadius will be invalid.",
                    this
                );
            }
        }
    }

    // Now derived instead of a serialized field
    public float OwnRadius
    {
        get
        {
            if(isRootMode)
            {
                return rootLayoutSettings != null
                    ? rootLayoutSettings.snowflakeCircumradius
                    : 0f;
            }

            return roomLayoutSettings != null
                ? roomLayoutSettings.snowflakeRadius
                : 0f;
        }
    }

    // Canonical spike-tip angle for a given letter index, matching TRUE koch N=1 snowflake geometry.
    // Use for root mode (root's 6 children sit at true spike-tip directions).
    public static float GetCanonicalSpikeAngleDegrees(int letterIndex)
    {
        return NormalizeAngle(90f + letterIndex * 60f);
    }

    // Edge-midpoint angle for a room's child slot,
    // matching KochSnowflakeMotifRenderer's/RoomBoundaryGenerator's actual drawn hexagon convention
    public static float GetCanonicalEdgeMidpointAngleDegrees(int roomLetterIndex, int childSlot)
    {
        float rotationAngle = roomLetterIndex * 60f;  // positive, as originally written
        return NormalizeAngle(rotationAngle + 30f + 60f * childSlot);
    }

    private static float NormalizeAngle(float angleDegrees)
    {
        float normalized = angleDegrees % 360f;

        if(normalized < 0f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    // Public result struct - everything caller needs to correctly place & scale child
    // targetRadius is ABSOLUTE - not yet divided into local scale ratio
    // caller (KochMotifNode.AttachChild) must divide targetRadius by childs own KochMotifAligner.OwnRadius to get correct requiredLocalScale
    public struct ChildTransformData
    {
        public Vector3 localPosition;
        public float localRotationZDegrees;
        public float targetRadius;
        public int letterIndex;
    }

    // Root mode: get the local transform data for child letter `letterIndex` (0..5
    // Room mode: get the local transform data for child slot `childSlot` (0=prev, 1=self, 2=next)    
    public bool TryGetChildLocalTransform(
        int letterIndexOrChildSlot,
        out ChildTransformData result)
    {
        result = default;

        if(isRootMode)
        {
            return TryGetRootChildLocalTransform(
                letterIndexOrChildSlot,
                out result
            );
        }

        return TryGetRoomChildLocalTransform(
            letterIndexOrChildSlot,
            out result
        );
    }

    private bool TryGetRootChildLocalTransform(
        int letterIndex,
        out ChildTransformData result)
    {
        result = default;

        if(letterIndex < 0 ||
            letterIndex >= FractalNode.LetterNames.Length)
        {
            Debug.LogError(
                "KochMotifAligner (root mode): invalid letterIndex " +
                letterIndex,
                this
            );

            return false;
        }

        float angleDegrees =
            GetCanonicalSpikeAngleDegrees(letterIndex)
            + extraRotationOffsetDegrees;

        float orbitRadius =
            OwnRadius * rootChildOrbitRadiusRatio;

        float targetRadius =
            OwnRadius * rootChildTargetRadiusRatio;

        Vector2 direction =
            AngleToDirection(angleDegrees);

        result.localPosition =
            new Vector3(
                center.x + direction.x * orbitRadius,
                center.y + direction.y * orbitRadius,
                0f
            );

        result.localRotationZDegrees =
            angleDegrees - 90f;

        result.targetRadius =
            targetRadius;

        result.letterIndex =
            letterIndex;

        return true;
    }

    private bool TryGetRoomChildLocalTransform(
        int childSlot,
        out ChildTransformData result)
    {
        result = default;

        if(childSlot < 0 || childSlot > 2)
        {
            Debug.LogError(
                "KochMotifAligner (room mode): invalid childSlot " +
                childSlot +
                " - expected 0 (previous), 1 (self), or 2 (next).",
                this
            );

            return false;
        }

        int[] childLetters =
            FractalNode.GetChildLetters(roomLetterIndex);

        int letterIndex =
            childLetters[childSlot];

        float angleDegrees =
            GetCanonicalEdgeMidpointAngleDegrees(
                roomLetterIndex,
                childSlot
            )
            + extraRotationOffsetDegrees;

        // Edge-midpoint distance from center for a hexagon inscribed at radius `ownRadius`
         // perpendicular distance from the center to midpoint of a side, not full spike-tip radius
        float orbitRadius =
            OwnRadius * Mathf.Cos(30f * Mathf.Deg2Rad);

        float targetRadius =
            OwnRadius * roomChildTargetRadiusRatio;

        Vector2 direction =
            AngleToDirection(angleDegrees);

        result.localPosition =
            new Vector3(
                center.x + direction.x * orbitRadius,
                center.y + direction.y * orbitRadius,
                0f
            );

        result.localRotationZDegrees =
            angleDegrees - 90f;

        result.targetRadius =
            targetRadius;

        result.letterIndex =
            letterIndex;

        return true;
    }

    private static Vector2 AngleToDirection(float angleDegrees)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        );
    }
}
