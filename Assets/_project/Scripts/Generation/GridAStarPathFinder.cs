using System.Collections.Generic;
using UnityEngine;

// Based on https://www.youtube.com/playlist?list=PLFt_AvWsXl0cq5Umv3pMC9SPnKjfp9eGW

// GridAStarPathfinder - standard A* search over a RoomPathGrid's walkable cells
// given a grid, a start world position, and a goal world position, returns an ordered list of world-space waypoints from start to goal, or null if no path exists

// Used by EnemyController in place of straight-line MoveTowards() when routing around stealth walls is required (Chase/Search states)
// Patrol still uses the existing straight-line TryGetRandomWalkableWorldPoint() sampling, since patrol targets are always walkable open points
// and don't need to route around obstacles the same way a moving player target does
public static class GridAStarPathfinder
{
    private class Node
    {
        public Vector2Int Cell;
        public Node Parent;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
    }

    public static List<Vector2> FindPath(RoomPathGrid grid, Vector2 startWorld, Vector2 goalWorld)
    {
        if(grid == null || !grid.IsBuilt)
        {
            return null;
        }

        Vector2Int startCell = grid.WorldToCell(startWorld);
        Vector2Int goalCell = grid.WorldToCell(goalWorld);

        if(!grid.IsWalkable(goalCell))
        {
            return null;
        }

        Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();
        List<Node> openSet = new List<Node>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        Node startNode = new Node { Cell = startCell, GCost = 0f, HCost = Heuristic(startCell, goalCell) };
        allNodes[startCell] = startNode;
        openSet.Add(startNode);

        // Safety cap - prevent a runaway search from ever hard-freezing a frame if a room's grid is unexpectedly large
        int maxIterations = grid.Columns * grid.Rows;
        int iterations = 0;

        while(openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            Node current = openSet[0];
            for(int i = 1; i < openSet.Count; i++)
            {
                if(openSet[i].FCost < current.FCost ||
                   (openSet[i].FCost == current.FCost && openSet[i].HCost < current.HCost))
                {
                    current = openSet[i];
                }
            }

            if(current.Cell == goalCell)
            {
                return ReconstructPath(grid, current);
            }

            openSet.Remove(current);
            closedSet.Add(current.Cell);

            foreach(Vector2Int neighbourCell in grid.GetNeighbours(current.Cell))
            {
                if(closedSet.Contains(neighbourCell))
                {
                    continue;
                }

                float moveCost = current.GCost + Heuristic(current.Cell, neighbourCell);

                if(!allNodes.TryGetValue(neighbourCell, out Node neighbourNode))
                {
                    neighbourNode = new Node
                    {
                        Cell = neighbourCell,
                        GCost = moveCost,
                        HCost = Heuristic(neighbourCell, goalCell),
                        Parent = current
                    };

                    allNodes[neighbourCell] = neighbourNode;
                    openSet.Add(neighbourNode);
                }
                else if(moveCost < neighbourNode.GCost)
                {
                    neighbourNode.GCost = moveCost;
                    neighbourNode.Parent = current;

                    if(!openSet.Contains(neighbourNode))
                    {
                        openSet.Add(neighbourNode);
                    }
                }
            }
        }

        // Open set exhausted (or safety cap hit) without reaching goalCell - no valid route exists,
        // for example the player's position is fully walled off from the enemy's current position
        return null;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Vector2Int.Distance(a, b);
    }

    private static List<Vector2> ReconstructPath(RoomPathGrid grid, Node goalNode)
    {
        List<Vector2> path = new List<Vector2>();
        Node current = goalNode;

        while(current != null)
        {
            path.Add(grid.CellToWorld(current.Cell));
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }
}