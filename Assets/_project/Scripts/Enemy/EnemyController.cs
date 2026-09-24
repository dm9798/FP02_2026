// EnemyController: FSM for single robot enemy contained within one room prefab's physical bounds
// that perimeter/parent-edge blocking colliders RoomBoundaryGenerator component already builds (RoomBlocker layer)
// States: Patrol, Chase, Search, Attack
// Lifecycle: spawned once by its room prefab (RoomBoundaryGenerator, via Initialize()) and
// persists as a child of that room's permanent instance.
// Die() destroys this GameObject - since the room instance itself is never destroyed (only hidden/reused, per FUM's GetOrCreateChild)
// the room's own hierarchy is the sole record of "this enemy is dead" - no external tracking needed
//
// UPDATED for stealth mechanics: detection is now gated by line-of-sight (Physics2D.Raycast
// against sightBlockingLayers, which should include the new "StealthWall" layer and the room
// perimeter) as well as distance, so hiding behind a stealth wall genuinely breaks detection
// even within detectionRadius.
//
// Chase and Search now route through RoomPathGrid + GridAStarPathfinder instead of straight-line
// MoveTowards(), so the enemy actually navigates around stealth walls rather than sliding along
// them or getting stuck.


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    private enum State
    {
        Patrol,
        Chase,
        Search,
        Attack
    }

    [Header("Detection")]
    [Tooltip("Distance at which the enemy notices the player and starts chasing")]
    [SerializeField] private float detectionRadius = 4f;

    [Tooltip("Distance at which the enemy gives up the chase and returns to patrolling")]
    [SerializeField] private float loseInterestRadius = 6f;

    [Header("Line of Sight (Stealth)")]
    [Tooltip("Layers that block line of sight: RoomBlocker layer.")]
    [SerializeField] private LayerMask sightBlockingLayers;

    [Header("Search (lost sight of player)")]
    [SerializeField] private float maxSearchTime = 3f;

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

    [Header("Pathfinding (Chase/Search)")]
    [Tooltip("Distance at which the current A* waypoint is considered 'reached' and the enemy advances to the next one")]
    [SerializeField] private float waypointArrivalTolerance = 0.25f;
    [Tooltip("Seconds between path re-requests while actively chasing")]
    [SerializeField] private float pathRepathInterval = 0.5f;

    [Header("Health")]
    [Tooltip("Total hit points - the enemy dies once currentHealth reaches zero.")]
    [SerializeField] private float maxHealth = 30f;

    [Header("Hit Flash")]
    [Tooltip("Color the sprite flashes to when taking damage.")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float hitFlashDuration = 0.12f;

    [Header("Death Flash + Fade")]
    [SerializeField] private Color deathFlashColor = Color.red;
    [SerializeField] private int deathFlashCount = 3;
    [SerializeField] private float deathFlashInterval = 0.08f;
    [SerializeField] private float deathFadeDuration = 0.6f;

    [Header("Stuck Detection (Chase/Search)")]
    [Tooltip("measure whether the enemy is making real progress while chasing/searching")]
    [SerializeField] private float stuckCheckInterval = 1f;
    [Tooltip("Minimum dist the enemy must actually travel within stuckCheckInterval to NOT be considered stuck")]
    [SerializeField] private float stuckMovementThreshold = 1f;
    [Tooltip("How many consecutive stuck windows in a row before the enemy gives up chasing and returns to Patrol")]
    [SerializeField] private int stuckWindowsBeforeGivingUp = 2;
    [Tooltip("After giving up due to being stuck, how long (seconds) enemy ignores the player before re-enter Chase")]
    [SerializeField] private float stuckGiveUpCooldown = 2f;

    [Header("Stuck Detection (Patrol)")]
    [Tooltip("measure whether the enemy is making real progress toward its current patrol target")]
    [SerializeField] private float patrolStuckCheckInterval = 1f;
    [Tooltip("Minimum dist the enemy must actually travel within patrolStuckCheckInterval to not be considered stuck")]
    [SerializeField] private float patrolStuckMovementThreshold = 0.3f;

    private float patrolStuckCheckTimer;
    private float patrolAccumulatedPathLength;
    private Vector2 patrolLastFramePosition;

    private float stuckGiveUpCooldownTimer;

    private float stuckCheckTimer;
    private Vector2 lastFramePosition;
    private float accumulatedPathLength;
    private int consecutiveStuckWindows;

    private float currentHealth;

    private Rigidbody2D rb;
    private Transform playerTransform;
    private RoomBoundaryGenerator ownerRoom;
    private RoomPathGrid pathGrid;

    // EnemyController never references concrete movement/attack type directly - only via interface
    private IEnemyMovement movement;
    private IEnemyAttack attack;

    private State currentState = State.Patrol;
    private Vector2 currentPatrolTarget;
    private bool hasValidPatrolTarget;
    private float patrolWaitTimer;
    private float attackCooldownTimer;
    private bool isDead;

    // shared by Chase and Search the currently active A* path (world-space waypoints) and
    // which waypoint index the enemy is walking towards,
    // plus a timer gating how often a fresh path is requested while the target (player, or last known position) keeps moving
    private List<Vector2> currentPath;
    private int currentWaypointIndex;
    private float repathTimer;

    private Vector2 lastKnownPlayerPosition;
    private float searchTimer;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private Coroutine hitFlashRoutine;
    private Collider2D[] colliders;

    private EnemyAnimationController animationController;

    public float CurrentSpeed
    {
        get; private set;
    }

    public bool IsChasing => currentState == State.Chase;

    public Vector2 CurrentFacingDirection { get; private set; } = Vector2.right;

    // Exposes isDead read-only, so EnemyAnimationController can poll it every frame
    public bool IsDead => isDead;

    // Called by component that spawns this enemy (RoomBoundaryGenerator) immediately after
    // Instantiate(), mirroring same "Initialize() before Start()" pattern already used vy that component
    public void Initialize(RoomBoundaryGenerator room, Transform player)
    {
        ownerRoom = room;
        playerTransform = player;

        if(ownerRoom != null)
        {
            pathGrid = ownerRoom.GetComponentInChildren<RoomPathGrid>(true);

            if(pathGrid == null)
            {
                Debug.LogWarning(
                    name + ": owner room has no RoomPathGrid - Chase/Search will fall back to " +
                    "straight-line movement and may get stuck on stealth walls.",
                    this
                );
            }
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // resolve movement/attack components. Both are required!!!
        movement = GetComponent<IEnemyMovement>();
        attack = GetComponent<IEnemyAttack>();
        currentHealth = maxHealth;

        animationController = GetComponent<EnemyAnimationController>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>();

        if(spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }

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

        accumulatedPathLength += Vector2.Distance(rb.position, lastFramePosition);
        lastFramePosition = rb.position;

        patrolAccumulatedPathLength += Vector2.Distance(rb.position, patrolLastFramePosition);
        patrolLastFramePosition = rb.position;

        switch(currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;

            case State.Chase:
                UpdateChase();
                break;

            case State.Search:
                UpdateSearch();
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

    // returns false if anything on sightBlockingLayers (StealthWall, room
    // perimeter/RoomBlocker) sits between this enemy and the player, regardless of raw distance
    // A player standing behind a stealth wall within detectionRadius will correctly NOT be detected
    private bool HasLineOfSightToPlayer()
    {
        if(playerTransform == null)
        {
            return false;
        }

        Vector2 origin = transform.position;
        Vector2 target = playerTransform.position;
        float distance = Vector2.Distance(origin, target);

        if(distance <= 0.0001f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, (target - origin).normalized, distance, sightBlockingLayers);
        return hit.collider == null;
    }

    private void UpdatePatrol()
    {
        float distanceToPlayer = DistanceToPlayer();

        if(stuckGiveUpCooldownTimer > 0f)
        {
            stuckGiveUpCooldownTimer -= Time.deltaTime;
        }
        else if(distanceToPlayer <= detectionRadius && HasLineOfSightToPlayer())
        {
            EnterChase();
            return;
        }

        patrolWaitTimer += Time.deltaTime;

        if(!hasValidPatrolTarget)
        {
            CurrentSpeed = 0f;

            if(patrolWaitTimer >= maxPatrolWaitTime)
            {
                PickNewPatrolTarget();
            }

            return;
        }

        float distanceToTarget = Vector2.Distance(rb.position, currentPatrolTarget);

        if(distanceToTarget <= patrolArrivalTolerance || patrolWaitTimer >= maxPatrolWaitTime)
        {
            PickNewPatrolTarget();
            return;
        }


        // if the enemy hasn't made real progress toward currentPatrolTarget within
        // patrolStuckCheckInterval return immediately so THIS frame doesn't also call
        // MoveTowards toward the now-stale currentPatrolTarget value        
        if(CheckPatrolStuckAndRetarget())
        {
            return;
        }

        CurrentSpeed = patrolSpeed;
        UpdateFacingTowards(currentPatrolTarget);

        if(movement != null)
            movement.MoveTowards(rb, currentPatrolTarget, patrolSpeed);
    }
    private void ResetStuckTracking()
    {
        stuckCheckTimer = 0f;
        accumulatedPathLength = 0f;
        consecutiveStuckWindows = 0;
    }

    private void EnterChase()
    {
        currentState = State.Chase;
        currentPath = null;
        currentWaypointIndex = 0;
        repathTimer = 0f;
        ResetStuckTracking();
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

        bool canSeePlayer = HasLineOfSightToPlayer();

        if(!canSeePlayer)
        {
            lastKnownPlayerPosition = playerTransform.position;
            searchTimer = 0f;
            currentPath = null;
            currentWaypointIndex = 0;
            repathTimer = 0f;
            currentState = State.Search;
            ResetStuckTracking();
            return;
        }

        if(distanceToPlayer <= attackRange)
        {
            currentState = State.Attack;
            attackCooldownTimer = 0f;
            return;
        }

        CurrentSpeed = chaseSpeed;
        UpdateFacingTowards(playerTransform.position);

        if(CheckStuckAndGiveUpIfNeeded())
        {
            return;
        }

        FollowPathTowards(playerTransform.position, chaseSpeed);
    }

    // Lost sight of the player mid-chase. Paths to their last known position and looks around
    // for maxSearchTime seconds before giving up and returning to Patrol
    // If line of sight is regained at any point (player peeks back out/enemy rounds the wall spots player again) resumes Chase
    private void UpdateSearch()
    {
        searchTimer += Time.deltaTime;

        if(DistanceToPlayer() <= detectionRadius && HasLineOfSightToPlayer())
        {
            EnterChase();
            return;
        }

        float distanceToLastKnown = Vector2.Distance(rb.position, lastKnownPlayerPosition);

        if(distanceToLastKnown <= patrolArrivalTolerance || searchTimer >= maxSearchTime)
        {
            currentState = State.Patrol;
            PickNewPatrolTarget();
            return;
        }

        CurrentSpeed = chaseSpeed;
        UpdateFacingTowards(lastKnownPlayerPosition);

        if(CheckStuckAndGiveUpIfNeeded())
        {
            return;
        }

        FollowPathTowards(lastKnownPlayerPosition, chaseSpeed);
    }

    // Shared helper (used by Patrol, Chase, Search, Attack) that updates CurrentFacingDirection
    // from the direction toward whatever target position that state is currently moving towards
    // (or facing, in Attack's case)
    private void UpdateFacingTowards(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - rb.position;

        if(direction.sqrMagnitude > 0.0001f)
        {
            CurrentFacingDirection = direction.normalized;
        }
    }

    // Shared by Chase and Search. Periodically requests a fresh A* path to targetWorldPos
    // (re-requesting every pathRepathInterval seconds rather than every frame, since exact
    // re-pathing on every frame is wasteful for a target that only moves gradually), then walks
    // the current path one waypoint at a time using the existing IEnemyMovement implementation   
    private void FollowPathTowards(Vector2 targetWorldPos, float speed)
    {
        if(pathGrid == null)
        {
            if(movement != null)
            {
                movement.MoveTowards(rb, targetWorldPos, speed);
            }

            return;
        }

        repathTimer -= Time.deltaTime;

        if(currentPath == null || repathTimer <= 0f)
        {
            currentPath = GridAStarPathfinder.FindPath(pathGrid, rb.position, targetWorldPos);
            currentWaypointIndex = 0;
            repathTimer = pathRepathInterval;
        }

        if(currentPath == null || currentPath.Count == 0)
        {
            // No valid route currently exists (e.g. target cell blocked/unreachable)
            return;
        }

        if(currentWaypointIndex >= currentPath.Count)
        {
            return;
        }

        Vector2 waypoint = currentPath[currentWaypointIndex];
        float distanceToWaypoint = Vector2.Distance(rb.position, waypoint);

        if(distanceToWaypoint <= waypointArrivalTolerance)
        {
            currentWaypointIndex++;

            if(currentWaypointIndex >= currentPath.Count)
            {
                return;
            }

            waypoint = currentPath[currentWaypointIndex];
        }

        if(movement != null)
        {
            movement.MoveTowards(rb, waypoint, speed);
        }
    }

    // Returns true if the enemy has given up due to being stuck
    private bool CheckStuckAndGiveUpIfNeeded()
    {
        stuckCheckTimer += Time.deltaTime;

        if(stuckCheckTimer < stuckCheckInterval)
        {
            return false;
        }

        float pathLengthThisWindow = accumulatedPathLength;

        stuckCheckTimer = 0f;
        accumulatedPathLength = 0f;

        if(pathLengthThisWindow >= stuckMovementThreshold)
        {
            consecutiveStuckWindows = 0;
            return false;
        }

        consecutiveStuckWindows++;

        if(consecutiveStuckWindows < stuckWindowsBeforeGivingUp)
        {
            return false;
        }

        consecutiveStuckWindows = 0;
        currentPath = null;
        currentWaypointIndex = 0;
        currentState = State.Patrol;
        stuckGiveUpCooldownTimer = stuckGiveUpCooldown;
        PickNewPatrolTarget();
        return true;
    }

    // Returns true if the enemy failed to make meaningful progress toward currentPatrolTarget
    // within patrolStuckCheckInterval
    private bool CheckPatrolStuckAndRetarget()
    {
        patrolStuckCheckTimer += Time.deltaTime;

        if(patrolStuckCheckTimer < patrolStuckCheckInterval)
        {
            return false;
        }

        float pathLengthThisWindow = patrolAccumulatedPathLength;

        patrolStuckCheckTimer = 0f;
        patrolAccumulatedPathLength = 0f;

        if(pathLengthThisWindow >= patrolStuckMovementThreshold)
        {
            return false;
        }

   
        PickNewPatrolTarget();
        return true;
    }

    private void UpdateAttack()
    {
        float distanceToPlayer = DistanceToPlayer();

        
        CurrentSpeed = 0f;

        if(playerTransform != null)
        {
            UpdateFacingTowards(playerTransform.position);
        }

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

            // Fires the AttackTrigger Animator parameter at the same moment the actual attack
            // logic runs, mirroring PlayerAnimationController's OnAttack event pattern
            if(animationController != null)
            {
                animationController.PlayAttack();
            }

            attackCooldownTimer = attackCooldown;
        }
    }

    private void PickNewPatrolTarget()
    {
        patrolWaitTimer = 0f;
        patrolStuckCheckTimer = 0f;
        patrolAccumulatedPathLength = 0f;

        if(ownerRoom == null)
        {
            hasValidPatrolTarget = false;
            return;
        }

        if(ownerRoom.TryGetRandomWalkableWorldPoint(out Vector2 worldPoint))
        {
            currentPatrolTarget = worldPoint;
            hasValidPatrolTarget = true;
        }
        else
        {
            hasValidPatrolTarget = false;
        }
    }

    // Applies damage to this enemy
    // Once currentHealth reaches zero or below, triggers Die() callers should call THIS method, not Die() directly, so health is always respected consistently
    public void TakeDamage(float amount)
    {
        if(isDead || amount <= 0f)
            return;

        currentHealth -= amount;

        if(currentHealth <= 0f)
        {
            Die();
            return;
        }

      

        if(spriteRenderer == null)
        {
            return;
        }

        if(hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            spriteRenderer.color = baseColor;
        }

        hitFlashRoutine = StartCoroutine(HitFlashCoroutine());
    }

    // Public method to destroy gameObject outright since the OWNING room instance is never destroyed (only hidden/reused across revisits)
    // this permanently removes the enemy from that room's hierarchy
    public void Die()
    {
        if(isDead)
            return;

        isDead = true;

       
        CurrentSpeed = 0f;

        // tell RoomDirector this enemy is gone, before Destroy() runs, so the room-cleared check
        // always sees a consistent state
        if(ownerRoom != null)
        {
            RoomDirector director = ownerRoom.GetComponent<RoomDirector>();

            if(director != null)
            {
                director.NotifyEnemyDefeated(this);
            }
        }

        // FOR LATER.. code block to trigger death VFX/loot as independent objects here if/when needed
        if(hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }

        // Stop the enemy acting like a physical/dangerous obstacle immediately, while the flash+fade plays out
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.angularVelocity = 0f;

        foreach(var collider in colliders)
        {
            if(collider != null)
            {
                collider.enabled = false;
            }
        }

        if(spriteRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(DeathFlashThenFadeCoroutine());
    }

    private IEnumerator HitFlashCoroutine()
    {
        spriteRenderer.color = hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        spriteRenderer.color = baseColor;
        hitFlashRoutine = null;
    }

    private IEnumerator DeathFlashThenFadeCoroutine()
    {
        for(int i = 0; i < deathFlashCount; i++)
        {
            spriteRenderer.color = deathFlashColor;
            yield return new WaitForSeconds(deathFlashInterval);

            spriteRenderer.color = baseColor;
            yield return new WaitForSeconds(deathFlashInterval);
        }

        float elapsed = 0f;
        Color fadeStartColor = baseColor;

        while(elapsed < deathFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / deathFadeDuration;

            Color faded = fadeStartColor;
            faded.a = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = faded;

            yield return null;
        }

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

        // Visualize the current A* path (if any) for easy debugging in the Scene view
        if(currentPath != null)
        {
            Gizmos.color = Color.magenta;
            for(int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }

            foreach(Vector2 waypoint in currentPath)
            {
                Gizmos.DrawSphere(waypoint, 0.08f);
            }
        }
    }
}
