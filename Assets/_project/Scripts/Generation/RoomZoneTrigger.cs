using UnityEngine;

public class RoomZoneTrigger : MonoBehaviour
{
    public string roomName;
    public bool isReturnEdge;
    public int targetLetterIndex = -1;
    public FractalNode ownerNode;
    public FractalUniverseManager universeManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.CompareTag("Player"))
            return;

        Debug.Log(
            "Player triggered edge: " +
            roomName +
            ", isReturnEdge=" +
            isReturnEdge
        );

        if(universeManager == null)
        {
            Debug.LogError(
                "No FractalUniverseManager assigned to " +
                name,
                this
            );

            return;
        }

        // read edge's own EdgeCollider2D.points, transform to world space,
        // project the player's position onto the segment
        float crossingT = CalculateCrossingT(other);

        if(isReturnEdge)
        {
            universeManager.RequestTraverseToParent(crossingT, other.transform);
        }
        else
        {
            universeManager.RequestTraverseToChild(
                targetLetterIndex,
                crossingT,
                other.transform
            );
        }
    }

    // helper for child-edge (isReturnEdge==false) and parent -edge (isReturnEdge==true) crossing
    // Calculates where along this edge in world space coords the collider currently is
    // EdgeCollider2D .points on this gameobj .points are in local space, so need transforming to world space
    private float CalculateCrossingT(Collider2D other)
    {
        float crossingT = 0.5f; // default to midpoint

        EdgeCollider2D edgeCollider = GetComponent<EdgeCollider2D>();

        if(edgeCollider != null && edgeCollider.points != null && edgeCollider.points.Length >= 2)
        {
            Vector2 worldStart = transform.TransformPoint(edgeCollider.points[0]);
            Vector2 worldEnd = transform.TransformPoint(edgeCollider.points[1]);

            Vector2 edgeVector = worldEnd - worldStart;
            float edgeLengthSquared = edgeVector.sqrMagnitude;

            if(edgeLengthSquared > 0.0001f)
            {
                Vector2 toOther = (Vector2)other.transform.position - worldStart;

                // Project onto the edge line then clamp to the segment itself
                // (0 = worldStart, 1 = worldEnd)
                crossingT = Mathf.Clamp01(Vector2.Dot(toOther, edgeVector) / edgeLengthSquared);
            }
        }
        else
        {
            Debug.LogWarning(
                "RoomZoneTrigger: " + name +
                " has no usable EdgeCollider2D.points - defaulting crossingT to midpoint (0.5).",
                this
            );
        }

        return crossingT;
    }
}
