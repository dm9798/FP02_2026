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

        if(isReturnEdge)
        {
            universeManager.RequestTraverseToParent();
        }
        else
        {
            universeManager.RequestTraverseToChild(
                targetLetterIndex
            );
        }
    }
}