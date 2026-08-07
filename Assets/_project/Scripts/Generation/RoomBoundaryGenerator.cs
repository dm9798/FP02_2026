using UnityEngine;

public class RoomBoundaryGenerator : MonoBehaviour
{
    public enum RoomLetter
    {
        A, B, C, D, E, F
    }

    [Header("Room Identity")]
    public RoomLetter roomLetter = RoomLetter.A;

    [Header("Hexagon Settings")]
    public Vector2 center = Vector2.zero;
    public float radius = 5f;

    //[Header("Parent Edge Tuning")]

    //public float gap = 0.75f;
    //public float widen = 0.3f; 

    //so parent edge autoscales with radius
    [Header("Parent Edge Tuning (as ratio of radius)")]
    public float gapRatio = 0.15f;   // was gap = 0.75f at radius = 5f
    public float widenRatio = -0.2f; // was widen = -2f at radius = 5f

    [Header("Zooms Transition Wiring")]
    public FractalZoomController zoomController;
    public FractalNode ownerNode;

    void Start()
    {
        GenerateEdges();
    }

    int GetRotationSteps()
    {
        string letter = roomLetter.ToString();
        return System.Array.IndexOf(FractalNode.LetterNames, letter);
    }

    Vector2 RotatePoint(Vector2 point, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Vector2 offset = point - center;
        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );
        return center + rotated;
    }

    void GenerateEdges()
    {
        int steps = GetRotationSteps();
        float rotationAngle = steps * 60f;

        Vector2[] baseHexPoints = new Vector2[4];
        for(int i = 0; i < 4; i++)
        {
            float angle = i * 60f;
            baseHexPoints[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        // Parent edge base points computed in SAME unrotated frame as Room A - baseline
        float widen = widenRatio * (radius * 1.67f); // or radius + 2 
        float gap = gapRatio * (radius * 1.67f); // or radius + 2
        Vector2 parentStartBase = new Vector2(baseHexPoints[3].x - widen, baseHexPoints[3].y - gap);
        Vector2 parentEndBase = new Vector2(baseHexPoints[0].x + widen, baseHexPoints[0].y - gap);

        // Now rotate ALL points together, including parent edge
        Vector2[] hexPoints = new Vector2[4];
        for(int i = 0; i < 4; i++)
            hexPoints[i] = RotatePoint(baseHexPoints[i], rotationAngle);

        Vector2 parentStart = RotatePoint(parentStartBase, rotationAngle);
        Vector2 parentEnd = RotatePoint(parentEndBase, rotationAngle);

        int[] childLetters = FractalNode.GetChildLetters(steps);

        CreateEdge(hexPoints[0], hexPoints[1], FractalNode.LetterNames[childLetters[0]], isReturnEdge: false);
        CreateEdge(hexPoints[1], hexPoints[2], FractalNode.LetterNames[childLetters[1]], isReturnEdge: false);
        CreateEdge(hexPoints[2], hexPoints[3], FractalNode.LetterNames[childLetters[2]], isReturnEdge: false);

        CreateEdge(parentStart, parentEnd, "Parent", isReturnEdge: true);
    }

    void CreateEdge(Vector2 a, Vector2 b, string edgeName, bool isReturnEdge)
    {
        GameObject edgeObj = new GameObject($"Edge_{edgeName}");
        edgeObj.transform.parent = this.transform;

        EdgeCollider2D edge = edgeObj.AddComponent<EdgeCollider2D>();
        edge.points = new Vector2[] { a, b };
        edge.isTrigger = true;

        RoomZoneTrigger trigger = edgeObj.AddComponent<RoomZoneTrigger>();
        trigger.roomName = edgeName;
        trigger.isReturnEdge = isReturnEdge;
        trigger.ownerNode = ownerNode;
        trigger.zoomController = zoomController;

        LineRenderer line = edgeObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        if(isReturnEdge)
        {
            line.startColor = Color.red;
            line.endColor = Color.red;
        }
    }
}