using System;
using System.Collections;
using UnityEngine;


// PlayerAttack - fire a projectile in one of 8 directions (N/NE/E/SE/S/SW/W/NW) on Left-Mouse-Click
// aimed toward the player's CURRENT FACING (last non-zero movement direction, exposed via PlayerMovement)
[RequireComponent(typeof(TestPlayerMovement))]
public class PlayerAttack : MonoBehaviour
{
    // Event raised right after a shot successfully fires (prefab assigned, cooldown elapsed)
    // PlayerAnimationController subscribes to this to trigger the Attack animator blend tree  
    public event Action OnAttack;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("World-space offset from the player's position where projectiles spawn")]
    [SerializeField] private float spawnDistanceFromPlayer = 0.5f;

    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileDamage = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSfx;
    [Tooltip("Randomizes pitch slightly per-shot")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    //[Header("Firing")]
    //[SerializeField] private float fireCooldown = 0.05f;

    [Header("Attack Lock")]
    [Tooltip("How long movement is locked while the attack animation plays. Should match Attack clip's length.")]
    [SerializeField] private float attackLockDuration = 0.25f;

    private TestPlayerMovement playerMovement;
    private float cooldownTimer;

    private static readonly Vector2[] CompassDirections =
    {
        new Vector2(1f, 0f),
        new Vector2(0.7071f, 0.7071f),
        new Vector2(0f, 1f),
        new Vector2(-0.7071f, 0.7071f),
        new Vector2(-1f, 0f),
        new Vector2(-0.7071f, -0.7071f),
        new Vector2(0f, -1f),
        new Vector2(0.7071f, -0.7071f)
    };

    private void Awake()
    {
        playerMovement = GetComponent<TestPlayerMovement>();
    }

    private void Update()
    {
        cooldownTimer -= Time.deltaTime;

        //if(Input.GetKeyDown(KeyCode.Space) && cooldownTimer <= 0f)
        //{
        //    Fire();
        //    cooldownTimer = fireCooldown;
        //} 

        // left mouse button attack
        // switched from space as not all keyboard works (up-left arrow key movement while pressing space shoot)
        if(Input.GetMouseButtonDown(0))
        {
            Fire();
        }
    }

    private void Fire()
    {
        if(projectilePrefab == null)
        {
            Debug.LogError(
                name + ": PlayerAttack has no projectilePrefab assigned - cannot fire.",
                this
            );

            return;
        }

        OnAttack?.Invoke();
        PlayShootSfx();

        StopAllCoroutines();
        StartCoroutine(LockMovementDuringAttack());

        Vector2 fireDirection = SnapToNearestCompassDirection(
            playerMovement.CurrentFacingDirection
        );

        Vector2 spawnPosition = (Vector2)transform.position + fireDirection * spawnDistanceFromPlayer;

        GameObject projectileInstance = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        ProjectileController projectileController =
            projectileInstance.GetComponent<ProjectileController>();

        if(projectileController == null)
        {
            Debug.LogError(
                name + ": projectilePrefab \"" + projectilePrefab.name +
                "\" has no ProjectileController component - destroying it.",
                this
            );

            Destroy(projectileInstance);
            return;
        }

        projectileController.Initialize(fireDirection, projectileSpeed, projectileDamage);

        
    }

    private void PlayShootSfx()
    {
        if(audioSource == null || shootSfx == null)
        {
            return;
        }

        audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(shootSfx);
    }

    // Finds whichever of the 8 compass directions has the smallest angle to rawDirection
    // (equivalent to largest dot product, since all vectors are unit length)
    private Vector2 SnapToNearestCompassDirection(Vector2 rawDirection)
    {
        Vector2 normalizedRaw = rawDirection.normalized;

        Vector2 bestDirection = CompassDirections[0];
        float bestDot = Vector2.Dot(normalizedRaw, CompassDirections[0]);

        for(int i = 1; i < CompassDirections.Length; i++)
        {
            float dot = Vector2.Dot(normalizedRaw, CompassDirections[i]);

            if(dot > bestDot)
            {
                bestDot = dot;
                bestDirection = CompassDirections[i];
            }
        }

        return bestDirection;
    }

    private IEnumerator LockMovementDuringAttack()
    {
        playerMovement.SetMovementLocked(true);
        yield return new WaitForSeconds(attackLockDuration);
        playerMovement.SetMovementLocked(false);
    }
}