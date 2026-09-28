using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// GameOverUI - RReads RunStatsSnapshot (captured by PlayerHealth right before this scene loaded) and displays final run stats
// plus wires up and "Try Again" and "Return to Menu" buttons

public class GameOverUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Scene Names (must match Build Settings exactly)")]
    [SerializeField] private string gameplaySceneName = "SampleScene";
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Labels")]
    [SerializeField] private string dungeonDepthLabel = "Dungeon Depth Reached";
    [SerializeField] private string roomsVisitedLabel = "Rooms Visited";
    [SerializeField] private string monstersSlainLabel = "Monsters Slain";

    private void Start()
    {
        DisplayStats();
    }

    private void DisplayStats()
    {
        if(statsText == null)
        {
            Debug.LogWarning(
                name + ": GameOverUI has no statsText assigned - run stats will not display.",
                this
            );

            return;
        }

        statsText.text =
            dungeonDepthLabel + ": " + RunStatsSnapshot.DungeonDepthReached + "\n" +
            roomsVisitedLabel + ": " + RunStatsSnapshot.RoomsVisited + "\n" +
            monstersSlainLabel + ": " + RunStatsSnapshot.MonstersSlain;
    }

    public void OnTryAgainPressed()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnReturnToMenuPressed()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}