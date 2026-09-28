using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// PlayerHealth - tracks current/max HP and exposes TakeDamage/Die for other scripts
// (enemy attacks, projectiles, hazards) to call into
// player death -> gameover transition
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 200f;

    [Header("Hit Flash")]
    [SerializeField] private Color hitFlashColor = Color.blue;

    [SerializeField] private float hitFlashDuration = 0.08f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip damageSfx;
    [SerializeField] private AudioClip deathSfx;
    [Tooltip("Randomizes pitch slightly")]
    [SerializeField] private Vector2 damagePitchRange = new Vector2(0.95f, 1.05f);

    [Header("Session Reset")]
    [SerializeField] private float reloadDelayAfterDeath = 1.5f;

    [Header("Scene Transition")]
    [SerializeField] private string gameOverSceneName = "GameOverScene";
    [SerializeField] private FractalUniverseManager universeManager;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float currentHealth;
    private Coroutine hitFlashRoutine;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    public event Action OnDamaged;
    public event Action OnHealed;
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

        PlayDamageSfx();

        OnDamaged?.Invoke();
    }

    public void Heal(float amount)
    {
        if(isDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        OnHealed?.Invoke();
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

        spriteRenderer.color = baseColor;

        PlayDeathSfx();

        OnDeath?.Invoke();

        StartCoroutine(ReloadAfterDeathCoroutine());
    }

    private void PlayDamageSfx()
    {
        if(audioSource == null || damageSfx == null)
        {
            return;
        }

        audioSource.pitch = UnityEngine.Random.Range(damagePitchRange.x, damagePitchRange.y);
        audioSource.PlayOneShot(damageSfx);
    }

    private void PlayDeathSfx()
    {
        if(audioSource == null || deathSfx == null)
        {
            return;
        }

        audioSource.pitch = 1f;
        audioSource.PlayOneShot(deathSfx);
    }

    private IEnumerator HitFlashCoroutine()
    {
        spriteRenderer.color = hitFlashColor;

        yield return new WaitForSeconds(hitFlashDuration);

        spriteRenderer.color = baseColor;
        hitFlashRoutine = null;
    }

    private IEnumerator ReloadAfterDeathCoroutine()
    {
        yield return new WaitForSeconds(reloadDelayAfterDeath);

        CaptureRunStats();

        SceneManager.LoadScene(gameOverSceneName);
    }

    private void CaptureRunStats()
    {
        if(universeManager == null)
        {
            universeManager = FindObjectOfType<FractalUniverseManager>();
        }

        if(universeManager == null)
        {
            Debug.LogWarning(
                name + ": could not find a FractalUniverseManager to capture run stats from - " +
                "Game Over scene will show default/zero values.",
                this
            );

            RunStatsSnapshot.Capture(0, 0, 0);
            return;
        }

        RunStatsSnapshot.Capture(
            universeManager.CurrentLevel,
            universeManager.RoomsVisited,
            universeManager.TotalMonstersSlain
        );
    }
}