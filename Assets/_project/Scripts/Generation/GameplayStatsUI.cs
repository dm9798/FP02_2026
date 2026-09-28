using UnityEngine;
using TMPro;

// GameplayStatsUI - displays in a fixed top-right screen corner
// updated every frame from FractalUniverseManager's public stats

[RequireComponent(typeof(TextMeshProUGUI))]
public class GameplayStatsUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    [Header("Labels")]
    [SerializeField] private string dungeonDepthLabel = "Dungeon Depth";
    [SerializeField] private string roomsVisitedLabel = "Rooms Visited";
    [SerializeField] private string monstersSlainLabel = "Monsters Slain";

    private TextMeshProUGUI statsText;

    private void Awake()
    {
        statsText = GetComponent<TextMeshProUGUI>();

        if(universeManager == null)
        {
            universeManager = FindObjectOfType<FractalUniverseManager>();

            if(universeManager == null)
            {
                Debug.LogError(
                    name + ": GameplayStatsUI has no FractalUniverseManager assigned and " +
                    "none was found in the scene - stats will not display.",
                    this
                );
            }
        }
    }

    private void LateUpdate()
    {
        if(universeManager == null || statsText == null)
        {
            return;
        }

       
        statsText.text =
            dungeonDepthLabel + ": " + universeManager.CurrentLevel + "\n" +
            roomsVisitedLabel + ": " + universeManager.RoomsVisited + "\n" +
            monstersSlainLabel + ": " + universeManager.TotalMonstersSlain;
    }
}