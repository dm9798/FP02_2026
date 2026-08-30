
using UnityEngine;

// OrthodoxAttack - the default IEnemyAttack implementation.
// Outstandin work is to wire attackDamage into the project's actual player health/damage interface once that
// system exists - this currently only logs, since no player health component has been built yet
public class OrthodoxAttack : MonoBehaviour, IEnemyAttack
{
    [Tooltip("Damage dealt per attack. Wired to whatever player health component/interface the " +
        "project uses once it exists - see Attack().")]
    [SerializeField] private float attackDamage = 10f;

    public void Attack(Transform self, Transform player)
    {
        Debug.Log(
            self.name + " attacks the player for " + attackDamage + " damage.",
            self
        );
    }
}