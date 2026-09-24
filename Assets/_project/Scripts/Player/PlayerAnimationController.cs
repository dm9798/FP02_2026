using UnityEngine;

// Responsible for Animator's Direction / IsMoving / IsRunning / AttackTrigger / DamageTrigger / DeathTrigger parameters
// and the SpriteRenderer's sprite selection, based on TestPlayerMovement.cs.
// Subscribes to PlayerAttack.OnAttack and PlayerHealth.OnDamaged / OnDeath so animation stays in sync to each of those events
//
// Note for self: "Direction" must remain a FLOAT parameter in the Animator Controller, since
// every Blend Tree below is a 1D Blend Tree and 1D Blend Trees only accept float parameters
//
// Direction values must match the thresholds used in EVERY Blend Tree (Idle, Walk, Run, Attack,
// TakeDamage) - same 8 motion fields, in the same order, using the enum below:
// 0=Down, 1=DownRight, 2=Right, 3=UpRight, 4=Up, 5=UpLeft, 6=Left, 7=DownLeft
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(TestPlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]
public class PlayerAnimationController : MonoBehaviour
{
    private enum ClipDirection
    {
        Down = 0,
        DownRight = 1,
        Right = 2,
        UpRight = 3,
        Up = 4,
        UpLeft = 5,
        Left = 6,
        DownLeft = 7
    }

    [Header("Walk / Run Threshold")]
    [Tooltip("Seconds of keydown directional input held before Walk -> Run.")]
    [SerializeField] private float runHoldThreshold = 0.3f;

    private static readonly int DirectionParam = Animator.StringToHash("Direction");
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int IsRunningParam = Animator.StringToHash("IsRunning");
    private static readonly int AttackTriggerParam = Animator.StringToHash("AttackTrigger");
    private static readonly int DamageTriggerParam = Animator.StringToHash("DamageTrigger");
    private static readonly int DeathTriggerParam = Animator.StringToHash("DeathTrigger");

    private Animator animator;
    private TestPlayerMovement movement;
    private PlayerAttack playerAttack;
    private PlayerHealth playerHealth;
    private SpriteRenderer spriteRenderer;

    // Tracks how long directional input has been held continuously, to gate the Walk -> Run
    // promotion. Reset to 0 the instant input drops back to zero (per confirmed design: a fresh
    // press always starts as Walk again, it does not remember previous holds).
    private float continuousInputHoldTime;
    private bool isRunning;

    // Set true the instant DeathTriggerParam fires, and never cleared - once dead, always dead,
    // for the lifetime of this component/GameObject.
    private bool isDead;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<TestPlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
        playerHealth = GetComponent<PlayerHealth>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if(playerHealth == null)
        {
            Debug.LogError(
                name + ": PlayerAnimationController requires a PlayerHealth component on the " +
                "same GameObject to drive TakeDamage/Death animations - none found.",
                this
            );
        }
    }

    void OnEnable()
    {
        playerAttack.OnAttack += HandleAttack;

        if(playerHealth != null)
        {
            playerHealth.OnDamaged += HandleDamaged;
            playerHealth.OnDeath += HandleDeath;
        }
    }

    void OnDisable()
    {
        playerAttack.OnAttack -= HandleAttack;

        if(playerHealth != null)
        {
            playerHealth.OnDamaged -= HandleDamaged;
            playerHealth.OnDeath -= HandleDeath;
        }
    }

    void Update()
    {
        // Once dead, this component stops driving Direction/IsMoving/IsRunning entirely
        if(isDead)
        {
            return;
        }

        Vector2 facing = movement.CurrentFacingDirection;
        ClipDirection direction = ResolveDirection(facing);
        animator.SetFloat(DirectionParam, (float)direction);

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool hasInput = input.sqrMagnitude > 0.0001f;

        UpdateWalkRunState(hasInput);

        animator.SetBool(IsMovingParam, hasInput);
        animator.SetBool(IsRunningParam, isRunning);
    }

    // Implements Walk -> Run design:  SAME movement keys drive both states.
    // A fresh key press always starts as Walk.
    // If input is held continuously for runHoldThreshold or more, promoted to Run for as long as input held
    // Releasing input resets the hold timer, so the next press starts as Walk
    private void UpdateWalkRunState(bool hasInput)
    {
        if(!hasInput)
        {
            continuousInputHoldTime = 0f;
            isRunning = false;
            return;
        }

        continuousInputHoldTime += Time.deltaTime;

        if(continuousInputHoldTime >= runHoldThreshold)
        {
            isRunning = true;
        }
    }

    private void HandleAttack()
    {
        animator.SetTrigger(AttackTriggerParam);
        Debug.Log("HandleAttack fired");
    }

    // Subscribed to PlayerHealth.OnDamaged. Fires a brief interrupt-then-return animation
    private void HandleDamaged()
    {
        if(isDead)
        {
            return;
        }

        animator.SetTrigger(DamageTriggerParam);
    }

    // Subscribed to PlayerHealth.OnDeath
    private void HandleDeath()
    {
        if(isDead)
        {
            return;
        }

        isDead = true;

        animator.SetTrigger(DeathTriggerParam);
        animator.SetBool(IsMovingParam, false);
        animator.SetBool(IsRunningParam, false);

        if(movement != null)
        {
            movement.enabled = false;
        }

        if(playerAttack != null)
        {
            playerAttack.enabled = false;
        }
    }

    // Convert 8-way facing vector into 45-degree slices, one slice per compass direction
    private static ClipDirection ResolveDirection(Vector2 facing)
    {
        if(facing.sqrMagnitude < 0.0001f)
        {
            return ClipDirection.Down;
        }

        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        angle = (angle + 360f) % 360f;
        int bucket = Mathf.FloorToInt(((angle + 22.5f) % 360f) / 45f);

        switch(bucket)
        {
            case 0:
                return ClipDirection.Right;
            case 1:
                return ClipDirection.UpRight;
            case 2:
                return ClipDirection.Up;
            case 3:
                return ClipDirection.UpLeft;
            case 4:
                return ClipDirection.Left;
            case 5:
                return ClipDirection.DownLeft;
            case 6:
                return ClipDirection.Down;
            default:
                return ClipDirection.DownRight;
        }
    }
}