using UnityEngine;

// StraightLineMovement is the default IEnemyMovement implementation.
// Moves directly toward the target position at a constant speed, with no deviation
public class StraightLineMovement : MonoBehaviour, IEnemyMovement
{
    public void MoveTowards(Rigidbody2D rb, Vector2 targetPosition, float speed)
    {
        Vector2 currentPosition = rb.position;
        Vector2 toTarget = targetPosition - currentPosition;

        if(toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector2 moveDirection = toTarget.normalized;
        rb.MovePosition(currentPosition + moveDirection * speed * Time.deltaTime);
    }
}