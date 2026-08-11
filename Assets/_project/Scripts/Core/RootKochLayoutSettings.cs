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
    public float boundaryRadius = 3f;
    public float boundaryAngleOffset = 60f;
}