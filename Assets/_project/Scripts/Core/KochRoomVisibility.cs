using UnityEngine;

// KochRoomVisibility refactored to toggles a room's visibility AND activity with prefab-instantiation model
// hidden rooms are never destroyed - but cached
// revisiting rooms reuses same instance via FUManager GetOrCreateChild
// stops colliders/coroutines from running on entire hidden subtree
// unity makea child of inactive gameobj inactive too
public class KochRoomVisibility : MonoBehaviour
{
    [Header("Scoping")]

    private bool isVisible = true;

    public bool IsVisible
    {
        get
        {
            return isVisible;
        }
    }

    // Shows or hides gameobj (and everything nested beneath it)    
    public void SetVisualVisible(bool visible)
    {
        isVisible = visible;
        gameObject.SetActive(visible);
    }
}
