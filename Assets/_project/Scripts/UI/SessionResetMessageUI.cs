using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// SessionResetMessageUI - shows a brief full-screen message before the scene reloads,
// for BOTH reset triggers in this user testing session
// (20-room session limit in FractalUniverseManager, player death in PlayerHealth)
// Attached to Canvas UI game obj in scene
// FractalUniverseManager and PlayerHealth both hold ref to this component
// and call ShowMessageThenReload(...) instead of reloading the scene directly themselves
public class SessionResetMessageUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Timing")]
    [SerializeField] private float displayDuration = 4f;

    private void Awake()
    {
        if(messagePanel == null)
        {
            Debug.LogError(name + ": SessionResetMessageUI has no messagePanel assigned.", this);
        }
        else
        {
            messagePanel.SetActive(false);
        }

        if(messageText == null)
        {
            Debug.LogError(name + ": SessionResetMessageUI has no messageText assigned.", this);
        }
    }

    // Public entry point for both reset triggers
    // Shows the panel with the given message, waits displayDuration seconds then reloads the active scene
    public void ShowMessageThenReload(string message)
    {
        StartCoroutine(ShowMessageThenReloadCoroutine(message));
    }

    private IEnumerator ShowMessageThenReloadCoroutine(string message)
    {
        if(messageText != null)
        {
            messageText.text = message;
        }

        if(messagePanel != null)
        {
            messagePanel.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(displayDuration);

        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }
}