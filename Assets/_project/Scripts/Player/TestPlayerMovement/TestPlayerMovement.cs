using UnityEngine;

public class TestPlayerMovement : MonoBehaviour
{
    public float speed = 2f;
    Rigidbody2D rb;

    // last non-zero movement direction, normalized. Defaults to facing "up" top-down perspective
    private Vector2 currentFacingDirection = Vector2.up;

    // when true, FixedUpdate() ignores input and zeroes velocity, effectively freezing the player in place
    // Set by PlayerAttack while an attack animation is playing
    private bool movementLocked;


    // public read-only access for other scripts (PlayerAttack) via GetComponent
    public Vector2 CurrentFacingDirection
    {
        get
        {
            return currentFacingDirection;
        }
    }

    void Awake() => rb = GetComponent<Rigidbody2D>();

    // public so other systems (PlayerAttack, and later stun/knockback effects if needed)
    // can freeze/unfreeze movement
    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if(movementLocked)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }


    void FixedUpdate()
    {
        if(movementLocked)
        {
            return;
        }

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        rb.linearVelocity = input.normalized * speed;

        if(input.sqrMagnitude > 0.0001f)
        {
            currentFacingDirection = input.normalized;
        }
    }
}