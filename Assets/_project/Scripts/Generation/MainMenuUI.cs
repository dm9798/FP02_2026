using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Names (must match Build Settings exactly)")]
    [SerializeField] private string gameplaySceneName = "GamePlayScene";

    [Header("Overlay Panels")]
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject quitConfirmPanel;

    public void OnStartGamePressed()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnHowToPlayPressed()
    {
        if(howToPlayPanel == null)
        {
            Debug.LogWarning(name + ": howToPlayPanel is not assigned - cannot show How To Play.", this);
            return;
        }

        howToPlayPanel.SetActive(true);
    }

    public void OnHowToPlayClosePressed()
    {
        if(howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
        }
    }

    public void OnCreditsPressed()
    {
        if(creditsPanel == null)
        {
            Debug.LogWarning(name + ": creditsPanel is not assigned - cannot show Credits.", this);
            return;
        }

        creditsPanel.SetActive(true);
    }

    public void OnCreditsClosePressed()
    {
        if(creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }

    // Shows a confirmation panel rather than quitting immediately
    public void OnQuitPressed()
    {
        if(quitConfirmPanel == null)
        {
            Debug.LogWarning(
                name + ": quitConfirmPanel is not assigned - quitting immediately with no " +
                "confirmation.",
                this
            );

            QuitImmediately();
            return;
        }

        quitConfirmPanel.SetActive(true);
    }

    public void OnQuitConfirmed()
    {
        QuitImmediately();
    }

    public void OnQuitCancelled()
    {
        if(quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }
    }

    private void QuitImmediately()
    {
        Debug.Log("Quitting application.", this);

        // Note: Application.Quit() only works in a real build
        Application.Quit();
    }
}