
using UnityEngine;

// IEnemyAttack is the contract for WHAT an enemy does when its Attack state fires.
// EnemyController only decides WHEN to attack (range + cooldown) - it never contains attack logic itself
// Different enemy prefabs attach different concrete implementations
public interface IEnemyAttack
{
    // Called once each time the Attack state's cooldown fires.
    // Implementations decide what actually happens:
    // dealing contact damage directly, spawning a projectile, playing an animation/VFX ...
    void Attack(Transform self, Transform player);
}