using UnityEngine;
using UnityEngine.UI;

// PlayerHealthBarUI - screen-space UI health bar from existing PlayerHealth component
// reads PlayerHealth.cs public CurrentHealth MaxHealth/IsDead properties only
//
// Must be attached to to the HealthBar_Fill Image GameObject -  UI.
public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private PlayerHealth playerHealth;

    [SerializeField] private Image fillImage;

    [Header("Colour Feedback")]
    [SerializeField] private bool useColorGradient = true;
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;

    private void Reset()
    {
        // auto-fill fillImage if this script is dropped
        // directly onto the fill Image GameObject.
        if(fillImage == null)
        {
            fillImage = GetComponent<Image>();
        }
    }

    private void Awake()
    {
        if(playerHealth == null)
        {
            Debug.LogError(
                name + ": PlayerHealthBarUI has no PlayerHealth assigned - " +
                "the health bar will not update.",
                this
            );
        }

        if(fillImage == null)
        {
            Debug.LogError(
                name + ": PlayerHealthBarUI has no fillImage assigned - " +
                "the health bar will not update.",
                this
            );
        }
    }

    private void Update()
    {
        if(playerHealth == null || fillImage == null)
        {
            return;
        }

        float normalizedHealth = playerHealth.MaxHealth > 0f
            ? playerHealth.CurrentHealth / playerHealth.MaxHealth
            : 0f;

        fillImage.fillAmount = normalizedHealth;

        if(useColorGradient)
        {
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, normalizedHealth);
        }
    }
}