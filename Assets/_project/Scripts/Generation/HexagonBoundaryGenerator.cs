using UnityEngine;

public class HexagonBoundaryGenerator : MonoBehaviour
{
    [Header("Hexagon Settings")]
    public Vector2 center = Vector2.zero;
    public float radius = 3f;

    [Header("Room Labels (clockwise order)")]
    public string[] roomNames = { "Room A", "Room B", "Room C", "Room D", "Room E", "Room F" };

    void Start()
    {
        GenerateEdges();
    }

    void GenerateEdges()
    {
        Vector2[] points = new Vector2[6];
        for(int i = 0; i < 6; i++)
        {
            float angle = 90f + i * 60f; // Start at top, to match koch snowflake orientation
            points[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        for(int i = 0; i < 6; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % 6];
            CreateEdgeCollider(a, b, roomNames[i]);
        }
    }

    void CreateEdgeCollider(Vector2 a, Vector2 b, string roomName)
    {
        GameObject edgeObj = new GameObject($"Edge_{roomName}");
        edgeObj.transform.parent = this.transform;

        EdgeCollider2D edge = edgeObj.AddComponent<EdgeCollider2D>();
        edge.points = new Vector2[] { a, b };

        RoomZoneTrigger triggerable = edgeObj.AddComponent<RoomZoneTrigger>();
        triggerable.roomName = roomName;

        edge.isTrigger = true;

        //LineRenderer for visual debugging
        LineRenderer line = edgeObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
    }
}