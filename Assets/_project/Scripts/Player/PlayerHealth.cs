using System.Collections;
using UnityEngine;

// PlayerHealth - tracks current/max HP and exposes TakeDamage/Die for other scripts
// (enemy attacks, projectiles, hazards) to call into.
//
// Handles two purely visual, code-driven feedback effects as sprite sheet has no dedicated hit/death frames:
// Hit reaction: brief color flash on the SpriteRenderer
// Death: a few rapid flashes, then a fade-out, then the GameObject is disabled
// Both run as coroutines layered on top -only touch spriteRenderer.color, never the Animator
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 200f;

    [Header("Hit Flash")]
    [SerializeField] private Color hitFlashColor = Color.blue;

    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Death Flash + Fade")]
    [SerializeField] private Color deathFlashColor = Color.red;
    [SerializeField] private int deathFlashCount = 3;
    [SerializeField] private float deathFlashInterval = 0.08f;
    [SerializeField] private float deathFadeDuration = 0.6f;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float currentHealth;
    private Coroutine hitFlashRoutine;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

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

        gameObject.SetActive(false);
    }
}
