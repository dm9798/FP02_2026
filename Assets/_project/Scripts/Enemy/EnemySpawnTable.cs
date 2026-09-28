// REPLACES GetEnemyPrefab() in EnemySpawnTable.cs with real weighted-random selection, using
// the existing spawnWeight field

using UnityEngine;

[CreateAssetMenu(fileName = "EnemySpawnTable", menuName = "Fractal Dungeon/Enemy Spawn Table")]
public class EnemySpawnTable : ScriptableObject
{
    [System.Serializable]
    public class EnemyEntry
    {
        public GameObject enemyPrefab;

        [Tooltip("Relative spawn weight. With multiple entries, each entry's chance of being " +
            "picked is its own weight divided by the sum of all valid entries' weights. Set " +
            "two entries to the SAME weight value for an equal 50/50 chance between them.")]
        [Min(0f)]
        public float spawnWeight = 1f;
    }

    [SerializeField] private EnemyEntry[] entries = new EnemyEntry[0];

    
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

      

       
        float totalWeight = 0f;

        for(int i = 0; i < entries.Length; i++)
        {
            if(entries[i] != null && entries[i].enemyPrefab != null && entries[i].spawnWeight > 0f)
            {
                totalWeight += entries[i].spawnWeight;
            }
        }

        if(totalWeight <= 0f)
        {
            Debug.LogWarning(
                name + ": EnemySpawnTable has entries, but none have a valid enemyPrefab AND " +
                "a positive spawnWeight - GetEnemyPrefab() returning null.",
                this
            );

            return null;
        }

        
        float randomPoint = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        for(int i = 0; i < entries.Length; i++)
        {
            if(entries[i] == null || entries[i].enemyPrefab == null || entries[i].spawnWeight <= 0f)
            {
                continue;
            }

            cumulativeWeight += entries[i].spawnWeight;

            if(randomPoint <= cumulativeWeight)
            {
                //Debug.Log("EnemySpawnTable picked: " + entries[i].enemyPrefab.name + " (randomPoint=" + randomPoint + ", totalWeight=" + totalWeight + ")");
                return entries[i].enemyPrefab;
            }
        }

       
        for(int i = entries.Length - 1; i >= 0; i--)
        {
            if(entries[i] != null && entries[i].enemyPrefab != null && entries[i].spawnWeight > 0f)
            {
                return entries[i].enemyPrefab;
            }
        }

        return null;
    }
}