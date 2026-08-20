using UnityEngine;

// KochRoomVisibility replaces GameObject.SetActive(false) for hiding a room's DRAWN content ince real parenting is in play (see KochMotifNode). 
// toggles ONLY the rendering (LineRenderer) and collision (EdgeCollider2D) components that live under this room's own VISUAL sub-object - never
// touching the room's MOTIF ANCHOR sub-object (where attached child rooms live). 
public class KochRoomVisibility : MonoBehaviour
{
    [Header("Scoping")]
    [SerializeField] private Transform roomVisualRoot;

    [Header("Collision Behaviour While Hidden")]
    [SerializeField] private bool disableCollidersWhenHidden = true;

    private LineRenderer[] cachedLineRenderers;
    private EdgeCollider2D[] cachedEdgeColliders;
    private bool isVisible = true;

    public bool IsVisible
    {
        get
        {
            return isVisible;
        }
    }

    private void EnsureCache()
    {
        if(roomVisualRoot == null)
        {
            Debug.LogError(
                "KochRoomVisibility: roomVisualRoot is not assigned.",
                this
            );

            return;
        }

        if(cachedLineRenderers == null)
        {
            cachedLineRenderers =
                roomVisualRoot.GetComponentsInChildren<LineRenderer>(true);
        }

        if(cachedEdgeColliders == null)
        {
            cachedEdgeColliders =
                roomVisualRoot.GetComponentsInChildren<EdgeCollider2D>(true);
        }
    }

    // Shows or hides this room's own drawn content (and optionally its boundary colliders)
    // WITHOUT ever calling SetActive on this room's GameObject or any ancestor/descendant -
    // safe to call regardless of whether room currently has children attached beneath it
    public void SetVisualVisible(bool visible)
    {
        EnsureCache();

        if(cachedLineRenderers != null)
        {
            foreach(LineRenderer lineRenderer in cachedLineRenderers)
            {
                if(lineRenderer != null)
                {
                    lineRenderer.enabled = visible;
                }
            }
        }

        if(disableCollidersWhenHidden &&
            cachedEdgeColliders != null)
        {
            foreach(EdgeCollider2D edgeCollider in cachedEdgeColliders)
            {
                if(edgeCollider != null)
                {
                    edgeCollider.enabled = visible;
                }
            }
        }

        isVisible = visible;
    }
}
