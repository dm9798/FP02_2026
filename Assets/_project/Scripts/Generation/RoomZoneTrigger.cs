using UnityEngine;

public class RoomZoneTrigger : MonoBehaviour
{
    public string roomName;
    public bool isReturnEdge;
    public int targetLetterIndex = -1;
    public FractalNode ownerNode;
    public FractalZoomController zoomController;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.CompareTag("Player"))
            return;

        Debug.Log(
            $"Player triggered edge: {roomName}, " +
            $"targetIndex={targetLetterIndex}, " +
            $"isReturnEdge={isReturnEdge}"
        );
    }
}