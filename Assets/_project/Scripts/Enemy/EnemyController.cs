// EnemyController: FSM for single robot enemy contained within one room prefab's physical bounds
// perimeter/parent-edge blocking colliders RoomBoundaryGenerator component already builds
// States: Patrol, Chase, Attack
// Lifecycle: spawned once by its room prefab (RoomBoundaryGenerator, via Initialize()) and
// persists as a child of that room's permanent instance. Die() destroys this GameObject - since
// the room instance itself is never destroyed (only hidden/reused, per FUM's GetOrCreateChild),
// the room's own hierarchy is the sole record of "this enemy is dead" - no external tracking needed

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    private enum State
    {
        Patrol,
        Chase,
        Attack
    }

    [Header("Detection")]
    [Tooltip("Distance at which the enemy notices the player and starts chasing")]
    [SerializeField] private float detectionRadius = 4f;

    [Tooltip("Distance at which the enemy gives up the chase and returns to patrolling")]
    [SerializeField] private float loseInterestRadius = 6f;

    [Header("Attack")]
    [Tooltip("Distance at which the enemy stops chasing and attacks instead")]
    [SerializeField] private float attackRange = 1f;

    [Tooltip("Seconds between attacks while the player remains in attackRange")]
    [SerializeField] private float attackCooldown = 1f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3f;

    [Tooltip("Distance at which a patrol target is considered 'reached' and a new one is picked.")]
    [SerializeField] private float patrolArrivalTolerance = 0.3f;

    [Tooltip("Max seconds to wait at a patrol target - or pursue unreachable one - before picking a new target")]
    [SerializeField] private float maxPatrolWaitTime = 4f;


    // NEW NEW NEW
    [Header("Health")]
    [Tooltip("Total hit points - the enemy dies once currentHealth reaches zero.")]
    [SerializeField] private float maxHealth = 30f;

    private float currentHealth;

    private Rigidbody2D rb;
    private Transform playerTransform;
    private RoomBoundaryGenerator ownerRoom;

    // EnemyController never references concrete movement/attack type directly - only via interface
    private IEnemyMovement movement;
    private IEnemyAttack attack;

    private State currentState = State.Patrol;
    private Vector2 currentPatrolTarget;
    private float patrolWaitTimer;
    private float attackCooldownTimer;
    private bool isDead;


    // Called by component that spawns this enemy (RoomBoundaryGenerator) immediately after
    // Instantiate(), mirroring same "Initialize() before Start()" pattern already used vy that component
    public void Initialize(RoomBoundaryGenerator room, Transform player)
    {
        ownerRoom = room;
        playerTransform = player;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // resolve movement/attack components. Both are required!!!
        movement = GetComponent<IEnemyMovement>();
        attack = GetComponent<IEnemyAttack>();
        currentHealth = maxHealth;

        if(movement == null)
        {
            Debug.LogError(
                name + " has no IEnemyMovement component attached (e.g. StraightLineMovement) " +
                "- this enemy will not move.",
                this
            );
        }

        if(attack == null)
        {
            Debug.LogError(
                name + " has no IEnemyAttack component attached (e.g. OrthodoxAttack) - this " +
                "enemy will never attack.",
                this
            );
        }

        
    }

    private void Start()
    {
        if(ownerRoom == null)
        {
            Debug.LogWarning(
                name + ": EnemyController.Initialize() was never called before Start() - " +
                "patrol targets cannot be sampled, this enemy will not move.",
                this
            );
        }

        // If no player Transform was supplied at spawn time, find it by tag instead.
        // Requires the player GameObject to be tagged "Player"
        if(playerTransform == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");

            if(playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
            else
            {
                Debug.LogWarning(
                    name + ": EnemyController has no player Transform assigned, and " +
                    "GameObject.FindWithTag(\"Player\") found nothing - detection/chase will " +
                    "never trigger. Make sure the player GameObject is tagged \"Player\".",
                    this
                );
            }
        }

        PickNewPatrolTarget();
    }

    private void Update()
    {
        if(isDead)
            return;

        switch(currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;

            case State.Chase:
                UpdateChase();
                break;

            case State.Attack:
                UpdateAttack();
                break;
        }
    }

    private float DistanceToPlayer()
    {
        if(playerTransform == null)
            return float.PositiveInfinity;

        return Vector2.Distance(transform.position, playerTransform.position);
    }

    private void UpdatePatrol()
    {
        float distanceToPlayer = DistanceToPlayer();

        if(distanceToPlayer <= detectionRadius)
        {
            currentState = State.Chase;
            return;
        }

        patrolWaitTimer += Time.deltaTime;

        float distanceToTarget = Vector2.Distance(rb.position, currentPatrolTarget);

        if(distanceToTarget <= patrolArrivalTolerance || patrolWaitTimer >= maxPatrolWaitTime)
        {
            PickNewPatrolTarget();
            return;
        }

        if(movement != null)
        {
            movement.MoveTowards(rb, currentPatrolTarget, patrolSpeed);
        }
    }

    private void UpdateChase()
    {
        float distanceToPlayer = DistanceToPlayer();

        if(distanceToPlayer > loseInterestRadius || playerTransform == null)
        {
            currentState = State.Patrol;
            PickNewPatrolTarget();
            return;
        }

        if(distanceToPlayer <= attackRange)
        {
            currentState = State.Attack;
            attackCooldownTimer = 0f;
            return;
        }


        if(movement != null)
        {
            movement.MoveTowards(rb, playerTransform.position, chaseSpeed);
        }
    }

    private void UpdateAttack()
    {
        float distanceToPlayer = DistanceToPlayer();

        if(distanceToPlayer > attackRange)
        {
            currentState = State.Chase;
            return;
        }

        attackCooldownTimer -= Time.deltaTime;

        if(attackCooldownTimer <= 0f)
        {
            if(attack != null)
            {
                attack.Attack(transform, playerTransform);
            }

            attackCooldownTimer = attackCooldown;
        }
    }

    private void PickNewPatrolTarget()
    {
        patrolWaitTimer = 0f;

        if(ownerRoom == null)
        {
            currentPatrolTarget = rb.position;
            return;
        }

        if(ownerRoom.TryGetRandomWalkableWorldPoint(out Vector2 worldPoint))
        {
            currentPatrolTarget = worldPoint;
        }
        else
        {
            // if fails, stay at the current position rather than moving somewhere unsafe/illegal
            currentPatrolTarget = rb.position;
        }
    }

    //NEW NEW NEW 
    // Applies damage to this enemy. Once currentHealth reaches zero or below, triggers Die() -
    // callers should call THIS method, not Die() directly, so health is always respected
    // consistently regardless of what's dealing the damage (projectiles today, anything else later).
    public void TakeDamage(float amount)
    {
        if(isDead)
            return;

        currentHealth -= amount;

        if(currentHealth <= 0f)
        {
            Die();
        }
    }

    // Public method to destroy gameObject outright
    // since the OWNING room instance is never destroyed (only hidden/reused across revisits), this permanently
    // removes the enemy from that room's hierarchy
    public void Die()
    {
        if(isDead)
            return;

        isDead = true;

        // May need to trigger death VFX/loot as independent objects here if/when needed
        // designed so NOT dependent on this GameObject surviving past this point
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(currentPatrolTarget, 0.2f);
    }

 
}