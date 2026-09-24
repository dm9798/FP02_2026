using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// PlayerHealth - tracks current/max HP and exposes TakeDamage/Die for other scripts
// (enemy attacks, projectiles, hazards) to call into
//
// NOTE: Death is now driven by PlayerAnimationController via the OnDeath event
//
// The session-reset-then-reload flow (SessionResetMessageUI, reloadDelayAfterDeath) has it own coroutine so it
// runs on a fixed delay after death rather than waiting on flash/fade timing - to also give
// the new Death animation clip time to actually play before the scene reloads
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 200f;

    [Header("Hit Flash")]
    [SerializeField] private Color hitFlashColor = Color.blue;

    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Session Reset")]
    [SerializeField] private float reloadDelayAfterDeath = 1.5f;

    [Header("Session Reset UI")]
    [Tooltip("Shows a brief message before reloading after the player dies")]
    [SerializeField] private SessionResetMessageUI sessionResetMessageUI;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float currentHealth;
    private Coroutine hitFlashRoutine;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    // NEW - subscribed to by PlayerAnimationController.HandleDamaged() to fire the DamageTrigger
    // animation. Raised once per TakeDamage() call that does not result in death (Die() raises
    // OnDeath instead, not OnDamaged, so the two are mutually exclusive per hit).
    public event Action OnDamaged;

    // NEW - subscribed to by PlayerAnimationController.HandleDeath() to fire the DeathTrigger
    // animation and disable movement/attack input. Raised exactly once, the first time Die() is
    // called - isDead guards against it firing more than once.
    public event Action OnDeath;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        baseColor = spriteRenderer.color;
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if(isDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if(currentHealth <= 0f)
        {
            Die();
            return;
        }

        if(hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            spriteRenderer.color = baseColor;
        }

        hitFlashRoutine = StartCoroutine(HitFlashCoroutine());

        OnDamaged?.Invoke();
    }

    public void Die()
    {
        if(isDead)
        {
            return;
        }

        isDead = true;
        currentHealth = 0f;

        if(hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }

        // Ensure sprite is left at its normal colour - the Death animation clip now handles all
        // death visual feedback, so we no longer want a stray hit-flash colour left applied
        // underneath it.
        spriteRenderer.color = baseColor;

        OnDeath?.Invoke();

        StartCoroutine(ReloadAfterDeathCoroutine());
    }

    private IEnumerator HitFlashCoroutine()
    {
        spriteRenderer.color = hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        spriteRenderer.color = baseColor;
        hitFlashRoutine = null;
    }

    // Replaces the old DeathFlashThenFadeCoroutine. Waits reloadDelayAfterDeath seconds - giving
    // the Death animation clip (triggered via OnDeath, above) time to actually play out - then
    // shows the session-reset message and reloads, exactly as before.
    private IEnumerator ReloadAfterDeathCoroutine()
    {
        yield return new WaitForSeconds(reloadDelayAfterDeath);

        if(sessionResetMessageUI != null)
        {
            sessionResetMessageUI.ShowMessageThenReload("You died.\n\nResetting...");
        }
        else
        {
            Debug.LogWarning(
                name + ": no SessionResetMessageUI assigned - reloading immediately with no message.",
                this
            );

            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }
    }
}