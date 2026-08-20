/* 
 * Procedural generation logic for the Koch Snowflake, based on the recursive construction of the Koch curve by Helge von Koch (1904).
 * Algorithm reference: https://en.wikipedia.org/wiki/Koch_snowflake
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
}