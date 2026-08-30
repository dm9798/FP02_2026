using UnityEngine;

// EnemySpawnTable is a ScriptableObject holding the rogues gallery of spawnable enemy prefabs for rooms
// The enemy spawning code in RoomBoundaryGenerator always asks this table for a prefab via GetEnemyPrefab() rather than holding a direct
// reference itself
// growing to multiple enemy types later (random selection, weighted by rarity, scaled by dungeon depth)
[CreateAssetMenu(fileName = "EnemySpawnTable", menuName = "Fractal Dungeon/Enemy Spawn Table")]
public class EnemySpawnTable : ScriptableObject
{
    //add, remove, configure different enemy prefabs and their respective spawn probabilities within Unity Inspector
    [System.Serializable]
    public class EnemyEntry
    {
        public GameObject enemyPrefab;

        [Tooltip("Relative spawn weight, for future weighted-random selection once there are " +
            "multiple entries. Not used if there's only one entry in the table.")]
        [Min(0f)]
        public float spawnWeight = 1f;
    }
      
    [SerializeField] private EnemyEntry[] entries = new EnemyEntry[0];

    // Returns a prefab to spawn, or null if the table is empty/misconfigured.
    // Currently just returns single entry
    // Function has to be changed later to incorporate multiple enemy types - may need a weighted-random pick using spawnWeight
    public GameObject GetEnemyPrefab()
    {
        if(entries == null || entries.Length == 0)
        {
            Debug.LogWarning(
                name + ": EnemySpawnTable has no entries - GetEnemyPrefab() returning null.",
                this
            );

            return null;
        }

        for(int i = 0; i < entries.Length; i++)
        {
            if(entries[i] != null && entries[i].enemyPrefab != null)
            {
                return entries[i].enemyPrefab;
            }
        }

        Debug.LogWarning(
            name + ": EnemySpawnTable has entries, but none have a valid enemyPrefab assigned.",
            this
        );

        return null;
    }
}