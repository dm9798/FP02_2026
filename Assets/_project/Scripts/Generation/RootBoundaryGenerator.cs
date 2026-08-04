using UnityEngine;

public class RootBoundaryGenerator : MonoBehaviour
{
    [Header("Root Hexagonal Edge Settings")]
    public Vector2 center = Vector2.zero;
    public float radius = 3f;

    [Header("Zoom Transition Wiring")]
    public FractalZoomController zoomController;

    void Start()
    {
        GenerateEdges();
    }

    void GenerateEdges()
    {
        Vector2[] points = new Vector2[6];
        for(int i = 0; i < 6; i++)
        {
            float angle = 60f + i * 60f;
            points[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        for(int i = 0; i < 6; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % 6];
            CreateEdgeCollider(a, b, FractalNode.LetterNames[i]);
        }
    }

    void CreateEdgeCollider(Vector2 a, Vector2 b, string roomName)
    {
        GameObject edgeObj = new GameObject($"Edge_{roomName}");
        edgeObj.transform.parent = this.transform;

        EdgeCollider2D edge = edgeObj.AddComponent<EdgeCollider2D>();
        edge.points = new Vector2[] { a, b };
        edge.isTrigger = true;

        RoomZoneTrigger trigger = edgeObj.AddComponent<RoomZoneTrigger>();
        trigger.roomName = roomName;
        trigger.isReturnEdge = false; // Root hexagon edges always lead deeper, never back to a parent
        trigger.ownerNode = null;     // Root does not itself need FractalNode identity 
        trigger.zoomController = zoomController;

        LineRenderer line = edgeObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
    }
}