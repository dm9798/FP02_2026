using UnityEngine;

// OrthodoxAttack - the default IEnemyAttack implementation.
// Wired to PlayerHealth.TakeDamage(), the project's player health/damage interface.
public class OrthodoxAttack : MonoBehaviour, IEnemyAttack
{
    [Tooltip("Damage dealt per attack.")]
    [SerializeField] private float attackDamage = 10f;

    public void Attack(Transform self, Transform player)
    {
        if(player == null)
        {
            return;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();

        if(playerHealth == null)
        {
            Debug.LogWarning(
                self.name + ": Attack() could not find a PlayerHealth component on " +
                player.name + " - no damage was applied.",
                self
            );

            return;
        }

        playerHealth.TakeDamage(attackDamage);
        //Debug.Log("player health currently at: " + playerHealth.CurrentHealth);
    }
}