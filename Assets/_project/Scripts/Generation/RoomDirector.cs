using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RoomBoundaryGenerator))]
public class RoomDirector : MonoBehaviour
{
    public enum RoomState
    {
        Unentered,
        Sealed,
        Cleared,
        Opened
    }

    // RENAMED from keyPrefab - same field, same purpose, now actively used
    [Header("Heart/Health Pickup")]
    [Tooltip("Spawned once, at a random point in the room, the moment the last enemy is defeated. Must have a HeartPickup component.")]
    [SerializeField] private GameObject heartPrefab;

    private RoomBoundaryGenerator boundaryGenerator;

    private RoomState currentState = RoomState.Unentered;

    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();

    private GameObject spawnedHeartInstance;

    public RoomState CurrentState => currentState;

    public bool HasActiveEnemies => activeEnemies.Count > 0;

    private void Awake()
    {
        boundaryGenerator = GetComponent<RoomBoundaryGenerator>();
    }

    public void NotifyPlayerEntered()
    {
        if(currentState != RoomState.Unentered)
        {
            return;
        }

        SealRoom();
    }

    public void RegisterSpawnedEnemy(EnemyController enemy)
    {
        if(enemy == null)
        {
            return;
        }

        activeEnemies.Add(enemy);
    }

   
    public void NotifyEnemyDefeated(EnemyController enemy)
    {
        activeEnemies.Remove(enemy);

        if(currentState != RoomState.Sealed)
        {
            return;
        }

        if(activeEnemies.Count == 0)
        {
            SpawnHeart();
            OpenRoom();
        }
    }

   //not used
    public void NotifyKeyCollected()
    {
        if(currentState != RoomState.Cleared)
        {
            Debug.LogWarning(
                name + ": NotifyKeyCollected() called while in state " + currentState +
                " - expected Cleared. Ignoring.",
                this
            );

            return;
        }

        OpenRoom();
    }

    private void SealRoom()
    {
        currentState = RoomState.Sealed;

        boundaryGenerator.SetAllChildEdgeCollisions(false);
        boundaryGenerator.SetParentEdgeBlocksPlayer(true);
        boundaryGenerator.SetParentTriggerEdgeEnabled(false);
        boundaryGenerator.PaintTraversalEdgesSealed();
    }

    private void OpenRoom()
    {
        currentState = RoomState.Opened;

        boundaryGenerator.RestoreAllEdgeCollisionSettings();
        boundaryGenerator.SetParentEdgeBlocksPlayer(false);
        boundaryGenerator.PaintTraversalEdgesOpen();
    }

   
    private void SpawnHeart()
    {
        if(heartPrefab == null)
        {
            Debug.LogWarning(
                name + ": room cleared but no heartPrefab assigned on RoomDirector - " +
                "no health pickup will spawn for this room.",
                this
            );

            return;
        }

        // SPawn anywhere in the room
        if(!boundaryGenerator.TryGetRandomWalkableWorldPoint(out Vector2 spawnPoint))
        {
            Debug.LogWarning(
                name + ": could not find a valid heart spawn point - falling back to this " +
                "GameObject's own position.",
                this
            );

            spawnPoint = transform.position;
        }

        
        spawnedHeartInstance = Instantiate(
            heartPrefab,
            spawnPoint,
            Quaternion.identity,
            transform
        );

        HeartPickup heartPickup = spawnedHeartInstance.GetComponent<HeartPickup>();

        if(heartPickup == null)
        {
            Debug.LogWarning(
                name + ": spawned heart prefab \"" + heartPrefab.name +
                "\" has no HeartPickup component - it will never heal the player or " +
                "destroy itself when touched.",
                this
            );
        }
        
    }
}