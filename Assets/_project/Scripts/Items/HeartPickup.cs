using UnityEngine;

// HeartPickup - a world-space item that heals the player on contact, then destroys itself.

[RequireComponent(typeof(Collider2D))]
public class HeartPickup : MonoBehaviour
{
    [Header("Healing")]
    [Tooltip("Amount of health restored to the player on pickup.")]
    [SerializeField] private float healAmount = 20f;

    [Header("Pickup Feedback (optional)")]
    [Tooltip("Optional sound effect played on pickup. Leave unassigned if not needed yet.")]
    [SerializeField] private AudioClip pickupSound;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!other.CompareTag("Player"))
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if(playerHealth == null)
        {
            Debug.LogWarning(
                name + ": HeartPickup touched something tagged \"Player\" but it has no " +
                "PlayerHealth component - cannot heal.",
                this
            );

            return;
        }

        // Capping to max health
        playerHealth.Heal(healAmount);

        if(pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        Destroy(gameObject);
    }
}