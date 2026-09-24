using System.Collections.Generic;
using UnityEngine;

// RoomPathGrid - walkability grid overlaid on a single room's actual fractal-shaped walkable polygon
// used for grid-based A* pathfinding by any EnemyController owned by this room
//
// Must atch RoomBoundaryGenerator's real geometry model: rooms are NOT simple rectangles, but jagged fractal polygons (RoomBoundaryGenerator.walkablePolygon, built in
// BuildWalkablePolygon() from the same perimeter chains used for the physical blocking colliders).
// A cell is walkable only if BOTH:
//   1) it falls inside walkablePolygon (RoomBoundaryGenerator.IsPointInPolygon), matching the
//      room's actual jagged perimeter rather than a rectangular approximation, and
//   2) it does not overlap anything on obstacleLayers (stealth walls - the "StealthWall" layer).
// The room's own perimeter/RoomBlocker colliders do NOT need to be in obstacleLayers, since
// walkablePolygon already excludes everything outside the fractal shape - obstacleLayers is only
// for INTERIOR obstacles placed inside an otherwise-walkable room (stealth walls).
//
// Owned by RoomBoundaryGenerator, built once in GenerateEdges() right after perimeter/stealth
// wall geometry exists and right before SpawnEnemyIfConfigured(), so every spawned enemy's
// Initialize() call already has a finished grid to resolve and path through.
public class RoomPathGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("World-space size of one grid cell. Smaller = more precise pathing, but slower to build/search")]
    [SerializeField] private float cellSize = 0.5f;

    [Tooltip("Layers treated as interior obstacles - stealth walls only")]
    [SerializeField] private LayerMask obstacleLayers;

    private bool[,] walkable;
    private int columns;
    private int rows;
    private Vector2 gridOriginLocal;
    private RoomBoundaryGenerator owner;
    private bool isBuilt;

    public bool IsBuilt => isBuilt;
    public int Columns => columns;
    public int Rows => rows;

    private void Awake()
    {
        owner = GetComponent<RoomBoundaryGenerator>();

        if(owner == null)
        {
            owner = GetComponentInParent<RoomBoundaryGenerator>();
        }
    }

    // Called by RoomBoundaryGenerator.GenerateEdges(), after walkablePolygon and any stealth
    // walls for this room both exist - mirrors  "Initialize()/Build before first use"
    // convention already used across codebase
    public void BuildGrid()
    {
        if(owner == null)
        {
            owner = GetComponent<RoomBoundaryGenerator>();
        }

        Vector2[] polygon = owner != null ? owner.GetWalkablePolygonLocalPoints() : null;

        if(polygon == null || polygon.Length < 3)
        {
            Debug.LogWarning(
                name + ": RoomPathGrid.BuildGrid() found no valid walkablePolygon on the owning " +
                "RoomBoundaryGenerator - grid will be empty, EnemyController will fall back to " +
                "straight-line movement for this room.",
                this
            );

            isBuilt = false;
            return;
        }

        float minX = polygon[0].x, maxX = polygon[0].x;
        float minY = polygon[0].y, maxY = polygon[0].y;

        for(int i = 1; i < polygon.Length; i++)
        {
            minX = Mathf.Min(minX, polygon[i].x);
            maxX = Mathf.Max(maxX, polygon[i].x);
            minY = Mathf.Min(minY, polygon[i].y);
            maxY = Mathf.Max(maxY, polygon[i].y);
        }

        gridOriginLocal = new Vector2(minX, minY);
        columns = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / cellSize));
        rows = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / cellSize));

        walkable = new bool[columns, rows];
        float overlapRadius = cellSize * 0.5f * 0.9f;

        for(int x = 0; x < columns; x++)
        {
            for(int y = 0; y < rows; y++)
            {
                Vector2 cellLocalPos = CellToLocal(x, y);
                Vector2 cellWorldPos = transform.TransformPoint(cellLocalPos);

                bool insidePolygon = RoomBoundaryGenerator.IsPointInPolygon(cellLocalPos, polygon);
                bool blockedByObstacle = Physics2D.OverlapCircle(cellWorldPos, overlapRadius, obstacleLayers) != null;

                walkable[x, y] = insidePolygon && !blockedByObstacle;
            }
        }

        isBuilt = true;
    }

    private Vector2 CellToLocal(int x, int y)
    {
        return gridOriginLocal + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
    }

    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        Vector2 local = (Vector2)transform.InverseTransformPoint(worldPos) - gridOriginLocal;
        int x = Mathf.Clamp(Mathf.FloorToInt(local.x / cellSize), 0, Mathf.Max(0, columns - 1));
        int y = Mathf.Clamp(Mathf.FloorToInt(local.y / cellSize), 0, Mathf.Max(0, rows - 1));
        return new Vector2Int(x, y);
    }

    public Vector2 CellToWorld(int x, int y)
    {
        return transform.TransformPoint(CellToLocal(x, y));
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return CellToWorld(cell.x, cell.y);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        if(!isBuilt || cell.x < 0 || cell.x >= columns || cell.y < 0 || cell.y >= rows)
        {
            return false;
        }

        return walkable[cell.x, cell.y];
    }

    // 8-directional neighbours, matching existing 8-directional movement/animation
    // convention (per PlayerAnimationController's ClipDirection enum)
    private static readonly Vector2Int[] NeighbourOffsets =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, 1),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(1, -1)
    };

    public IEnumerable<Vector2Int> GetNeighbours(Vector2Int cell)
    {
        foreach(Vector2Int offset in NeighbourOffsets)
        {
            Vector2Int neighbour = cell + offset;

            if(IsWalkable(neighbour))
            {
                yield return neighbour;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if(!isBuilt)
        {
            return;
        }

        for(int x = 0; x < columns; x++)
        {
            for(int y = 0; y < rows; y++)
            {
                Gizmos.color = walkable[x, y] ? new Color(0f, 1f, 0f, 0.15f) : new Color(1f, 0f, 0f, 0.35f);
                Gizmos.DrawCube(CellToWorld(x, y), Vector3.one * cellSize * 0.9f);
            }
        }
    }
}