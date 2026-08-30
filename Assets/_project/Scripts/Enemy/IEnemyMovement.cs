using UnityEngine;

// IEnemyMovement is the contract for how an enemy moves toward a target position.
// EnemyController component aka the FSM only ever calls MoveTowards() with WHERE to go and HOW FAST, never contains movement math itself
// Different enemy prefabs attach different concrete implementations
public interface IEnemyMovement
{
    // Called every frame EnemyController wants this enemy to move toward targetPosition (a patrol
    // waypoint, or the player's position during a chase).
    // rb is the enemy's own Rigidbody2D - implementations move it via rb.MovePosition()    
    void MoveTowards(Rigidbody2D rb, Vector2 targetPosition, float speed);
}