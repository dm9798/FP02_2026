using UnityEngine;
using UnityEngine.UI;

// EnemyHealthBar - a small floating health bar that follows its owning enemy around, positioned
// slightly above them. Reads health via EnemyController.HealthPercent01 (read-only, added
// alongside this component) every frame
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private EnemyController enemyController;

    [SerializeField] private Image fillImage;

    [Header("Follow Position")]
    [SerializeField] private Transform followTarget;

    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.1f, 0f);

    [Header("Visibility")]
    [SerializeField] private bool hideAtFullHealth = true;

    [SerializeField] private bool hideOnDeath = true;

    [Header("Fill Color (optional)")]
    [SerializeField] private bool colorByHealth = true;
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;

    [Header("Camera Facing")]
    [SerializeField] private bool faceCamera = false;

    private Canvas canvas;
    private Camera mainCamera;

    private void Awake()
    {
        if(enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }

        if(enemyController == null)
        {
            Debug.LogError(
                name + ": EnemyHealthBar has no EnemyController assigned and none was found " +
                "in its parents - this health bar will not function. Assign one in the Inspector.",
                this
            );
        }

        if(followTarget == null && enemyController != null)
        {
            followTarget = enemyController.transform;
        }

        if(fillImage == null)
        {
            Debug.LogError(
                name + ": EnemyHealthBar has no fillImage assigned - the bar will not visually " +
                "update. Assign the 'Fill' Image (Image Type = Filled) in the Inspector.",
                this
            );
        }

        canvas = GetComponent<Canvas>();

        if(faceCamera)
        {
            mainCamera = Camera.main;

            if(mainCamera == null)
            {
                Debug.LogWarning(
                    name + ": faceCamera is enabled but no Camera tagged MainCamera was found - " +
                    "the health bar will not rotate to face the camera.",
                    this
                );
            }
        }
    }

    private void LateUpdate()
    {
        if(enemyController == null || followTarget == null)
        {
            return;
        }

       
        transform.position = followTarget.position + worldOffset;

        if(faceCamera && mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }

        UpdateVisibility();
        UpdateFill();
    }

    private void UpdateVisibility()
    {
        bool isDead = enemyController.IsDead;
        float healthPercent = enemyController.HealthPercent01;

        bool shouldHide =
            (hideOnDeath && isDead) ||
            (hideAtFullHealth && healthPercent >= 1f && !isDead);

        SetVisible(!shouldHide);
    }

    private void SetVisible(bool visible)
    {
        if(canvas != null)
        {
            canvas.enabled = visible;
            return;
        }

        gameObject.SetActive(visible);
    }

    private void UpdateFill()
    {
        if(fillImage == null)
        {
            return;
        }

        float healthPercent = enemyController.HealthPercent01;
        fillImage.fillAmount = healthPercent;

        if(colorByHealth)
        {
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
        }
    }
}