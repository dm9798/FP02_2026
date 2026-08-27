/* 
 * Procedural generation logic for the Koch Snowflake, based on the recursive construction of the Koch curve by Helge von Koch (1904).
 * Algorithm reference: [https://en.wikipedia.org/wiki/Koch_snowflake](https://en.wikipedia.org/wiki/Koch_snowflake)
 */

using System.Collections.Generic;
using UnityEngine;

public static class KochMath
{
    public static void GenerateKochSegment(Vector2 a, Vector2 b, int depth, List<Vector2> outPoints)
    {
        if(depth == 0)
        {
            outPoints.Add(a);
            return;
        }

        Vector2 delta = (b - a) / 3f;
        Vector2 p1 = a + delta;
        Vector2 p3 = a + delta * 2f;

        float angleRad = -60f * Mathf.Deg2Rad;
        Vector2 rotated = new Vector2(
            delta.x * Mathf.Cos(angleRad) - delta.y * Mathf.Sin(angleRad),
            delta.x * Mathf.Sin(angleRad) + delta.y * Mathf.Cos(angleRad)
        );
        Vector2 peak = p1 + rotated;

        GenerateKochSegment(a, p1, depth - 1, outPoints);
        GenerateKochSegment(p1, peak, depth - 1, outPoints);
        GenerateKochSegment(peak, p3, depth - 1, outPoints);
        GenerateKochSegment(p3, b, depth - 1, outPoints);
    }

    public static Vector2[] GenerateSnowflake(Vector2 center, float radius, int depth)
    {
        var points = new List<Vector2>();
        Vector2[] triangle = new Vector2[3];
        for(int i = 0; i < 3; i++)
        {
            float angle = 90f + i * 120f;
            triangle[i] = center + radius * new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            );
        }

        for(int i = 0; i < 3; i++)
            GenerateKochSegment(triangle[i], triangle[(i + 1) % 3], depth, points);

        points.Add(points[0]); // Close the loop back to start
        return points.ToArray();
    }

    // shared point-classification test - extracted out of KochSnowflakeMotifRenderer
    // now shared with KochSnowflakeMotifRenderer and RoomBoundaryGenerator
    public static bool IsOutsideEdge(
        Vector2 point,
        Vector2 edgeStart,
        Vector2 edgeEnd,
        Vector2 referenceCenter)
    {
        Vector2 edgeDirection =
            edgeEnd - edgeStart;

        Vector2 toPoint =
            point - edgeStart;

        Vector2 toCenter =
            referenceCenter - edgeStart;

        float crossPoint =
            edgeDirection.x * toPoint.y
            - edgeDirection.y * toPoint.x;

        float crossCenter =
            edgeDirection.x * toCenter.y
            - edgeDirection.y * toCenter.x;

        return Mathf.Sign(crossPoint)
            != Mathf.Sign(crossCenter);
    }

    // clustering logic, refactored out of KochSnowflakeMotifRenderer.Draw().
    // Generates snowflake raw fractal points at provided depth   
    public static List<List<Vector2>> GetFilteredSnowflakeClusters(
        Vector2 center,
        float snowflakeRadius,
        int depth,
        Vector2[] hexPoints,
        Vector2 parentStart,
        Vector2 parentEnd)
    {
        List<Vector2> snowflakePoints =
            new List<Vector2>(GenerateSnowflake(center, snowflakeRadius, depth));

        List<List<Vector2>> clusters =
            new List<List<Vector2>>();

        List<Vector2> currentCluster = null;

        foreach(Vector2 point in snowflakePoints)
        {
            bool outsideAnyRoof =
                IsOutsideEdge(point, hexPoints[0], hexPoints[1], center)
                ||
                IsOutsideEdge(point, hexPoints[1], hexPoints[2], center)
                ||
                IsOutsideEdge(point, hexPoints[2], hexPoints[3], center);

            bool insideParent =
                !IsOutsideEdge(point, parentStart, parentEnd, center);

            bool keep = outsideAnyRoof || insideParent;

            if(keep)
            {
                if(currentCluster == null)
                {
                    currentCluster = new List<Vector2>();
                    clusters.Add(currentCluster);
                }

                currentCluster.Add(point);
            }
            else
            {
                currentCluster = null;
            }
        }

        return clusters;
    }
}