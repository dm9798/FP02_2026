using UnityEngine;

public class RoomZoneTrigger : MonoBehaviour
{
    public string roomName;
    public bool isReturnEdge;
    public FractalNode ownerNode;
    public FractalZoomController zoomController;

    void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.CompareTag("Player"))
            return;
        Debug.Log($"Player triggered edge: {roomName} (isReturnEdge={isReturnEdge})");
    }
}