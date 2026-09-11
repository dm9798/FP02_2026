using UnityEngine;

// ProjectileController - fully autonomous once fired
// PlayerAttack.cs instantiates a projectile prefab and calls Initialize() once
// from that point on, this script owns the projectile's entire lifecycle (movement, collision, self-destruction)
// with no further involvement from PlayerAttack.cs
// Explicit two-branch collision handling (perimeter vs. enemy)
[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileController : MonoBehaviour
{
    [Header("Perimeter Collision")]
    [SerializeField] private string perimeterLayerName = "RoomBlocker";

    [Header("Lifetime Safety Net")]
    [SerializeField] private float maxLifetimeSeconds = 5f;

    private Rigidbody2D rb;
    private float damageAmount;
    private int perimeterLayer;
    private float lifetimeTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        perimeterLayer = LayerMask.NameToLayer(perimeterLayerName);

        if(perimeterLayer == -1)
        {
            Debug.LogError(
                name + ": ProjectileController's perimeterLayerName \"" + perimeterLayerName +
                "\" does not exist - this projectile will never be destroyed by hitting the " +
                "room boundary.",
                this
            );
        }
    }

    // Called once by PlayerAttack immediately after Instantiate()
    // Sets the projectile flying in a fixed direction at a fixed speed - no per-frame targeting, just travels straigh
    public void Initialize(Vector2 direction, float speed, float damage)
    {
        rb.linearVelocity = direction.normalized * speed;
        damageAmount = damage;
    }

    private void Update()
    {
        lifetimeTimer += Time.deltaTime;

        if(lifetimeTimer >= maxLifetimeSeconds)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Branch 1 - hit the fractal perimeter: destroy, no damage dealt
        if(other.gameObject.layer == perimeterLayer)
        {
            Destroy(gameObject);
            return;
        }

        // Branch 2 - hit an enemy: deal damage via the enemy's own TakeDamage(), then destroy
        EnemyController enemyController = other.GetComponent<EnemyController>();

        if(enemyController != null)
        {
            enemyController.TakeDamage(damageAmount);
            Destroy(gameObject);
        }
    }
}