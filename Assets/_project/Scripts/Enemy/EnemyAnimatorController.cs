
using UnityEngine;

// EnemyAnimationController - owns the Animator, translates EnemyController's existing FSM state (Patrol/Chase/Search/Attack) and
// lifecycle events (Die) into Animator parameters.
// EnemyController only exposes read-only state (CurrentSpeed, IsChasing, CurrentFacingDirection,
// IsDead) and calls one public hook method (PlayAttack)

// Enemy spritesheets are 8-directional
// NOTE for Unity: Direction must be a FLOAT Animator parameter, exactly like the player's, since the Idle/Walk/Run/Attack/Die blend trees are bound to it and
// 1D Blend Trees only accept float parameters
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyController))]
public class EnemyAnimationController : MonoBehaviour
{
    // Matches PlayerAnimationController's ClipDirection convention exactly, using all 8 entries    
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

    private static readonly int DirectionParam = Animator.StringToHash("Direction");
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int IsRunningParam = Animator.StringToHash("IsRunning");
    private static readonly int AttackTriggerParam = Animator.StringToHash("AttackTrigger");
    private static readonly int IsDeadParam = Animator.StringToHash("IsDead");

    private Animator animator;
    private EnemyController enemyController;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        enemyController = GetComponent<EnemyController>();
    }

    private void Update()
    {
        if(!animator.isInitialized)
        {
            return;
        }

        if(enemyController.IsDead)
        {
            animator.SetBool(IsDeadParam, true);
            return;
        }

        bool isMoving = enemyController.CurrentSpeed > 0.01f;
        bool isRunning = enemyController.IsChasing;

        animator.SetBool(IsMovingParam, isMoving);
        animator.SetBool(IsRunningParam, isRunning);

        ClipDirection direction = ResolveDirection(enemyController.CurrentFacingDirection);
        animator.SetFloat(DirectionParam, (float)direction);
    }

    // Converts an 8-way facing vector into 45-degree slices, then maps each of the 8 slices onto
    // its own physical ClipDirection - 
    // Mirrors PlayerAnimationController.ResolveDirection()'s math exactly
    private static ClipDirection ResolveDirection(Vector2 facing)
    {
        if(facing.sqrMagnitude < 0.0001f)
        {
            return ClipDirection.Down;
        }

        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        angle = (angle + 360f) % 360f;

        int bucket = Mathf.FloorToInt((angle + 22.5f) % 360f / 45f);

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

    // Called by EnemyController.UpdateAttack() at the same point it already calls
    // attack.Attack(...) - fires once per successful attack, mirroring how
    // PlayerAnimationController.HandleAttack() subscribes to PlayerAttack.OnAttack.
    public void PlayAttack()
    {
        animator.SetTrigger(AttackTriggerParam);
    }
}