using UnityEngine;

public class RoomKochLayoutSettings : MonoBehaviour
{
    [Header("Room Geometry")]
    public Vector2 center = Vector2.zero;

    [Header("Room Scale")]
    public Vector3 normalLocalScale = Vector3.one;

    //world-space target - every room will animate to the SAME fixed screen/world spot when fully zoomed
    public Vector3 normalWorldPosition = Vector3.zero;

    [Header("Snowflake Geometry")]
    public float snowflakeRadius = 3f;

    [Header("Boundary Geometry")]
    // Was: public float boundaryRadius = 5f; (absolute, independent of snowflakeRadius - could drift)
    [Tooltip(
        "Boundary radius expressed as a ratio of snowflakeRadius, so it scales automatically " +
        "when snowflakeRadius changes. Default (5/3) preserves the original 5f absolute value " +
        "at the original snowflakeRadius of 3f."
    )]
    public float boundaryRadiusRatio = 5f / 3f;
    //public float boundaryRadiusRatio = 5f / 7f;

    // Derived - use this everywhere the old boundaryRadius field was read.
    public float BoundaryRadius
    {
        get
        {
            return snowflakeRadius * boundaryRadiusRatio;
        }
    }

    [Header("Parent Edge Tuning")]
    public float gapRatio = 0.45f;
    public float widenRatio = -0.1f;

    [Header("Room Orientation")]
    [Tooltip("Rotation is calculated from the room letter by the room rendering scripts.")]
    public float roomRotationOffset = 0f;

    [Header("Room Identity")]
    [Tooltip("Room letter index ordered as A, F, E, D, C, B.")]
    public int roomLetterIndex = 0;

    [Header("Child Motif Layout")]
    public float childMotifRadiusRatio = 1f / 3f;

    [Header("Child Motif Emerge Geometry")]
    // Was: public float childMotifEmergeRadius = 2.2f; (absolute, independent of snowflakeRadius)
    [Tooltip(
        "Ratio of snowflakeRadius used ONLY to calculate where level-2+ child motifs visually " +
        "emerge from on this room's edges (previous/self/next). Default (2.2/3) preserves the " +
        "original 2.2f absolute value at the original snowflakeRadius of 3f."
    )]
    public float childMotifEmergeRadiusRatio = 2.2f / 3f;

    // Derived - use this everywhere the old childMotifEmergeRadius field was read.
    public float ChildMotifEmergeRadius
    {
        get
        {
            return snowflakeRadius * childMotifEmergeRadiusRatio;
        }
    }
}
