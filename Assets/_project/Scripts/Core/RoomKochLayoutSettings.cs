using UnityEngine;

public class RoomKochLayoutSettings : MonoBehaviour
{
    [Header("Room Geometry")]
    public Vector2 center = Vector2.zero;

    [Header("Room Scale")]
    public Vector3 normalLocalScale = Vector3.one;

    [Header("Snowflake Geometry")]
    public float snowflakeRadius = 3f;

    [Header("Boundary Geometry")]    
    public float boundaryRadiusRatio = 5f / 3f;
    //public float boundaryRadiusRatio = 5f / 7f;

    // Derived - replacing boundaryRadius var
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
    public float childMotifEmergeRadiusRatio = 2.2f / 3f;

    public float ChildMotifEmergeRadius
    {
        get
        {
            return snowflakeRadius * childMotifEmergeRadiusRatio;
        }
    }
}
