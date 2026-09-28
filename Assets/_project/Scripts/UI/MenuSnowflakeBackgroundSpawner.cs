using System.Collections;
using UnityEngine;

public class MenuSnowflakeBackgroundSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject snowflakePrefab;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 spawnAreaMin = new Vector2(-8f, -4.5f);
    [SerializeField] private Vector2 spawnAreaMax = new Vector2(8f, 4.5f);

    [Header("Spawn Timing")]
    [SerializeField] private float spawnInterval = 1.2f;
    [SerializeField] private int maxConcurrentSnowflakes = 12;

    [Header("Randomized Recursion Depth")]
    [Tooltip("Inclusive range. Kept modest (0-4) since depth 5-6 gets extremely dense at small sizes.")]
    [SerializeField] private int minRecursionDepth = 1;
    [SerializeField] private int maxRecursionDepth = 4;

    [Header("Randomized Size")]
    [SerializeField] private float minRadius = 0.5f;
    [SerializeField] private float maxRadius = 2.5f;

    [Header("Randomized Pulse (grow/shrink)")]
    [SerializeField] private float minPulseAmplitude = 0.1f;
    [SerializeField] private float maxPulseAmplitude = 0.6f;
    [SerializeField] private float minPulseSpeed = 0.3f;
    [SerializeField] private float maxPulseSpeed = 1.2f;

    [Header("Randomized Rotation")]
    [SerializeField] private float minRotationSpeed = -30f;
    [SerializeField] private float maxRotationSpeed = 30f;

    [Header("Randomized Lifetime")]
    [SerializeField] private float minLifetime = 6f;
    [SerializeField] private float maxLifetime = 14f;

    private int currentCount;

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while(true)
        {
            yield return new WaitForSeconds(spawnInterval);

            if(currentCount < maxConcurrentSnowflakes && snowflakePrefab != null)
            {
                SpawnOne();
            }
        }
    }

    private void SpawnOne()
    {
        Vector2 position = new Vector2(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y)
        );

        GameObject instance = Instantiate(snowflakePrefab, transform);

        BackgroundKochSnowflake snowflake = instance.GetComponent<BackgroundKochSnowflake>();

        if(snowflake == null)
        {
            Debug.LogError(
                name + ": snowflakePrefab has no BackgroundKochSnowflake component - destroying it.",
                this
            );

            Destroy(instance);
            return;
        }

        int depth = Random.Range(minRecursionDepth, maxRecursionDepth + 1);
        float radius = Random.Range(minRadius, maxRadius);
        float amplitude = Random.Range(minPulseAmplitude, maxPulseAmplitude);
        float pulseSpeed = Random.Range(minPulseSpeed, maxPulseSpeed);
        float rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed);
        float lifetime = Random.Range(minLifetime, maxLifetime);

        snowflake.Initialize(position, depth, radius, amplitude, pulseSpeed, rotationSpeed, lifetime);

        currentCount++;
        StartCoroutine(DecrementCountAfter(lifetime));
    }

    private IEnumerator DecrementCountAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentCount--;
    }
}