using System.Collections.Generic;
using UnityEngine;


// Requires a Collider2D  on same GameObject, on a layer that collides with "RoomBlocker" in the Physics2D Layer Collision Matrix
// solid collision resolution and these OnCollision callbacks both depend on that being set up correctly
public class StraightLineMovement : MonoBehaviour, IEnemyMovement
{
   [SerializeField] private string solidWallLayerName = "RoomBlocker";

    private int solidWallLayer;

    // Tracks every currently-touched solid wall's contact normal, keyed by the OTHER collider involved
    private readonly Dictionary<Collider2D, Vector2> activeWallNormals = new Dictionary<Collider2D, Vector2>();

    private void Awake()
    {
        solidWallLayer = LayerMask.NameToLayer(solidWallLayerName);

        if(solidWallLayer == -1)
        {
            Debug.LogError(
                name + ": StraightLineMovement's solidWallLayerName \"" + solidWallLayerName +
                "\" does not exist - wall sliding cannot be detected, enemy may get stuck " +
                "against solid geometry instead of sliding along it.",
                this
            );
        }
    }

    public void MoveTowards(Rigidbody2D rb, Vector2 targetPosition, float speed)
    {
        Vector2 currentPosition = rb.position;
        Vector2 toTarget = targetPosition - currentPosition;

        if(toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector2 moveDirection = toTarget.normalized;

        // sliding wall case if currently touching one or more solid walls
        // remove the into-wall components the desired direction before moving, so enemy slides along the surface
        if(activeWallNormals.Count > 0)
        {
            Vector2[] normals = new Vector2[activeWallNormals.Count];
            activeWallNormals.Values.CopyTo(normals, 0);

            moveDirection = WallSlideHelper.GetSlideDirection(moveDirection, normals);

            if(moveDirection == Vector2.zero)
                return;
        }

        rb.MovePosition(currentPosition + moveDirection * speed * Time.deltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TrackContactIfSolidWall(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Re-captured every physics step since the contact normal can change slightly as the enemy continues moving along a curved/jagged chain of edges
        //Recursion depth geometry of fractal motif prefab room kept deliberately low, so efficient
        TrackContactIfSolidWall(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        activeWallNormals.Remove(collision.collider);
    }

    private void TrackContactIfSolidWall(Collision2D collision)
    {
        if(collision.collider.gameObject.layer != solidWallLayer)
            return;

        if(collision.contactCount == 0)
            return;

        // Average all contact points' normals for this collider as EdgeCollider2D chains can
        // report more than one contact point per physics step where two segments meet
        Vector2 averagedNormal = Vector2.zero;

        for(int i = 0; i < collision.contactCount; i++)
        {
            averagedNormal += collision.GetContact(i).normal;
        }

        averagedNormal = (averagedNormal / collision.contactCount).normalized;

        activeWallNormals[collision.collider] = averagedNormal;
    }
}