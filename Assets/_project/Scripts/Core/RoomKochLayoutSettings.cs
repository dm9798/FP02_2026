using UnityEngine;

public class RoomKochLayoutSettings : MonoBehaviour
{
    [Header("Room Geometry")]
    public Vector2 center = Vector2.zero;

    [Header("Snowflake Geometry")]
    public float snowflakeRadius = 3f;

    [Header("Boundary Geometry")]
    public float boundaryRadius = 5f;

    [Header("Parent Edge Tuning")]
    //public float gapRatio = 0.15f;    
    public float gapRatio = 0.45f;    
    public float widenRatio = -0.2f;

    [Header("Room Orientation")]
    [Tooltip(
        "Rotation is calculated from the room letter " +
        "by the room rendering scripts."
    )]
    public float roomRotationOffset = 0f;

    [Header("Room Identity")]
    [Tooltip(
    "Room letter index ordered as A, F, E, D, C, B."
)]
    public int roomLetterIndex = 0;

    [Header("Child Motif Layout")]
    public float childMotifRadiusRatio = 1f / 3f;
}