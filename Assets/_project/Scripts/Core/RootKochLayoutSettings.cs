using UnityEngine;

public class RootKochLayoutSettings : MonoBehaviour
{
    [Header("Shared Root Geometry")]
    public Vector2 center = Vector2.zero;
    public float snowflakeCircumradius = 5f;

    [Header("Root Orientation")]
    public float motifAngleOffset = 90f;

    [Header("Motif Layout")]
    public float motifRadiusRatio = 1f / 3f;
    public float motifOrbitRadiusRatio = 2f / 3f;

    [Header("Root Boundary Geometry")]
    [Tooltip("Boundary radius as a ratio of snowflakeCircumradius, so it scales automatically.")]
    public float boundaryRadiusRatio = 3f / 5f; // preserves the current 3f/5f default relationship
    public float boundaryAngleOffset = 60f;

    //Derived, read-only
    public float BoundaryRadius
    {
        get
        {
            return snowflakeCircumradius * boundaryRadiusRatio;
        }
    }
}