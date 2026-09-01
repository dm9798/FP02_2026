using UnityEngine;

//static utility for use by any IEnemyMovement implementation
// shared math for "collision sliding" to fix enemy ghosting-thru-walls bug with former "on trigger" approach
// given a movement/direction and the surface normal of a wall currently being touched, return a new direction
// with the into-wall component removed, leaving only the along-wall component
// lets enemy slide along wall while trying to close distance on player target, instead of freezing at wall contact

public static class WallSlideHelper
{
    // Calculate the desiredDirection onto the wall surface (perpendicular to wallNormal), removing
    // only the component pointing INTO the wall
    // If desiredDirection is already moving away from or parallel to the wall, it is returned unchanged
    public static Vector2 GetSlideDirection(Vector2 desiredDirection, Vector2 wallNormal)
    {
        if(wallNormal == Vector2.zero)
            return desiredDirection;

        float intoWallAmount = Vector2.Dot(desiredDirection, wallNormal);

        // Positive dot product means desiredDirection already points AWAY from the wall
        // (normal points outward from the surface) - nothing to remove
        if(intoWallAmount >= 0f)
            return desiredDirection;

        Vector2 intoWallComponent = wallNormal * intoWallAmount;
        Vector2 slideDirection = desiredDirection - intoWallComponent;

        // Renormalize - subtracting a component changes magnitude, and callers expect unit
        // direction vector times own speed value
        if(slideDirection.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        return slideDirection.normalized;
    }

    // overload method for case of touching MULTIPLE wall contacts at once
    // Applies sliding against each normal in sequence
    public static Vector2 GetSlideDirection(Vector2 desiredDirection, Vector2[] wallNormals)
    {
        Vector2 result = desiredDirection;

        for(int i = 0; i < wallNormals.Length; i++)
        {
            result = GetSlideDirection(result, wallNormals[i]);
        }

        return result;
    }
}