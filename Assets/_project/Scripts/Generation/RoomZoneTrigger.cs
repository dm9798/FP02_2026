using UnityEngine;

public class RoomZoneTrigger : MonoBehaviour
{
    public string roomName;

    void OnTriggerEnter2D(Collider2D other)
    {
        if(other.CompareTag("Player"))
            Debug.Log($"Player walked into hexagon edge leading to: {roomName}");
    }
}