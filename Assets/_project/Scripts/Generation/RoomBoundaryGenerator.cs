using UnityEngine;

public class RoomBoundaryGenerator : MonoBehaviour
{
    [Header("Hexagon-Derived Shape")]
    public Vector2 center = Vector2.zero;
    public float radius = 3f;

    [Header("Return Edge (Parent)")]
    public Vector2 returnEdgeStart = new Vector2(-4f, -3f);
    public Vector2 returnEdgeEnd = new Vector2(4f, -3f);

    [Header("Room Identity")]
    public FractalNode node; // Assigned by the manager when this room is generated
    public FractalZoomController zoomController;

    void Start()
    {
        if(node == null)
            node = new FractalNode { letter = 0 }; // Hardcode "A" for this test

        GenerateEdges();
    }

    void GenerateEdges()
    {
        int[] childLetters = node.GetChildLetters(); // [prev, self, next]

        // Same hexagon point formula as HexagonBoundaryGenerator, just only need 4 consecutive points
        Vector2[] hexPoints = new Vector2[4];
        for(int i = 0; i < 4; i++)
        {
            float angle = i * 60f; 

            hexPoints[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        // 3 consecutive edges sharing vertices, like a cut-out hexagon segment
        CreateEdge(hexPoints[0], hexPoints[1], FractalNode.LetterNames[childLetters[0]], isReturnEdge: false); // prev (F) — right diagonal
        CreateEdge(hexPoints[1], hexPoints[2], FractalNode.LetterNames[childLetters[1]], isReturnEdge: false); // self (A) — top
        CreateEdge(hexPoints[2], hexPoints[3], FractalNode.LetterNames[childLetters[2]], isReturnEdge: false); // next (B) — left diagonal

        // Separate, disconnected return edge back to parent
        // hard coded values, will need to be redone when scale changed
        float gap = 0.75f;
        float widen = 0.5f; // extend outward both sides, has to be fine tuned
        
        Vector2 parentStart = new Vector2(hexPoints[3].x - widen, hexPoints[3].y - gap);
        Vector2 parentEnd = new Vector2(hexPoints[0].x + widen, hexPoints[0].y - gap);
        CreateEdge(parentStart, parentEnd, "Parent", isReturnEdge: true);
    }

    void CreateEdge(Vector2 a, Vector2 b, string label, bool isReturnEdge)
    {
        GameObject edgeObj = new GameObject($"Edge_{label}");
        edgeObj.transform.parent = this.transform;

        EdgeCollider2D edge = edgeObj.AddComponent<EdgeCollider2D>();
        edge.points = new Vector2[] { a, b };
        edge.isTrigger = true;

        RoomZoneTrigger trigger = edgeObj.AddComponent<RoomZoneTrigger>();
        trigger.roomName = label;
        trigger.isReturnEdge = isReturnEdge;
        trigger.zoomController = zoomController;
        trigger.ownerNode = node;

        LineRenderer line = edgeObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
    }
}