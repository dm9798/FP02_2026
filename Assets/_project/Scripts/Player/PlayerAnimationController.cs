using UnityEngine;

// Reponsible for Animator's Direction / IsMoving / AttackTrigger parameters and the
// SpriteRenderer's flipX, based on PlayerMovement.cs
// Subscribes to PlayerAttack.OnAttack so animation stays in sync with whatever conditions/cooldowns actually bound firing
//
// Note for self: "Direction" must be a FLOAT parameter in the Animator Controller, since
// the Idle/Run/Attack Blend Trees are all bound to it and 1D Blend Trees only accept float parameters
// Previously incorrectly created as an int
//
// Direction values must match the thresholds used in every Blend Tree
// (Idle, Run, Attack): 0=Down, 1=DownRight, 2=Right, 3=UpRight, 4=Up
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
        Up = 4
    }

    private static readonly int DirectionParam = Animator.StringToHash("Direction");
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int AttackTriggerParam = Animator.StringToHash("AttackTrigger");

    private Animator animator;
    private TestPlayerMovement movement;
    private PlayerAttack playerAttack;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<TestPlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable()
    {
        playerAttack.OnAttack += HandleAttack;
    }

    void OnDisable()
    {
        playerAttack.OnAttack -= HandleAttack;
    }

    void Update()
    {
        Vector2 facing = movement.CurrentFacingDirection;

        (ClipDirection direction, bool flipX) = ResolveDirection(facing);

        animator.SetFloat(DirectionParam, (float)direction);
        spriteRenderer.flipX = flipX;

        // IsMoving is driven off raw input each frame
        // not CurrentFacingDirection (which deliberately keeps its last value at rest)
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        animator.SetBool(IsMovingParam, input.sqrMagnitude > 0.0001f);
    }

    private void HandleAttack()
    {
        animator.SetTrigger(AttackTriggerParam);
    }

    // Convert 8-way facing vector into 45-degree slices
    // then map each of the 8 slices onto one of the 5 physical clip directions
    // flipping horizontally for the 3 directions that don't have their own clips (Left, UpLeft, DownLeft)
    private static (ClipDirection, bool) ResolveDirection(Vector2 facing)
    {
        if(facing.sqrMagnitude < 0.0001f)
        {
            return (ClipDirection.Down, false);
        }

        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        angle = (angle + 360f) % 360f;
        int bucket = Mathf.FloorToInt(((angle + 22.5f) % 360f) / 45f);

        switch(bucket)
        {
            case 0:
                return (ClipDirection.Right, false);      // Right
            case 1:
                return (ClipDirection.UpRight, false);    // Up-Right
            case 2:
                return (ClipDirection.Up, false);         // Up
            case 3:
                return (ClipDirection.UpRight, true);     // Up-Left (mirrored)
            case 4:
                return (ClipDirection.Right, true);       // Left (mirrored)
            case 5:
                return (ClipDirection.DownRight, true);   // Down-Left (mirrored)
            case 6:
                return (ClipDirection.Down, false);       // Down
            default:
                return (ClipDirection.DownRight, false); // Down-Right
        }
    }
}
