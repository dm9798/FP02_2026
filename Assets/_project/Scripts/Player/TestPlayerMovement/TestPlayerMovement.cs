using UnityEngine;

public class TestPlayerMovement : MonoBehaviour
{
    public float speed = 2f;
    Rigidbody2D rb;

    // last non-zero movement direction, normalized. Defaults to facing "up" top-down perspective
    private Vector2 currentFacingDirection = Vector2.up;

    // public read-only access for other scripts (PlayerAttack) via GetComponent
    public Vector2 CurrentFacingDirection
    {
        get
        {
            return currentFacingDirection;
        }
    }

    void Awake() => rb = GetComponent<Rigidbody2D>();

    //JUST TO TEST DAMAGE ON PLAYER
    //private void Update()
    //{
    //    if(Input.GetKeyDown(KeyCode.Space))
    //    {
    //        GetComponent<PlayerHealth>().TakeDamage(25f);
    //    }
    //}

    void FixedUpdate()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        rb.linearVelocity = input.normalized * speed;

        // only update facing when there IS actual movement input this frame
        // if input is zero (player standing still), currentFacingDirection deliberately keeps its last value
        if(input.sqrMagnitude > 0.0001f)
        {
            currentFacingDirection = input.normalized;
        }        
    }
}