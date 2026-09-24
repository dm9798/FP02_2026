// RoomDirector - owns the room-level MICRO-LOOP state machine: what happens once a player enters a room, until they're allowed back out
// component only decides WHEN state should change and calls the appropriate public methods on those other components
// Lives as a sibling component on the same GameObject as RoomBoundaryGenerator (the "Room_Visual_X" child object)
// Unentered State: room has never been entered. Edges use their generation-time defaults (whatever RoomBoundaryGenerator baked in at GenerateEdges()
// Sealed State: player just crossed in via a child edge. Child edge collisions all FALSE freely re-enterable at the child-motif level, parent edge collision TRUE
// (parentEdgeBlocksPlayer), all traversal edges (parent + children) painted red. Enemy(s) tracked as "active" - room is not yet cleared
// Cleared State: last tracked enemy died. Key object spawned somewhere in the room's non-child interior. Room stays sealed (edges still red/blocking) until the key is collected
// Opened State: key collected. All traversal edges turn green, parent edge unblocks, child edge collisions restored to their original per-room settings

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

    [Header("Key Pickup")]
    [Tooltip("The key prefab to spawn once this room's enemies are all defeated. Must have a " +
        "KeyPickup component that reports collection back to this RoomDirector.")]
    [SerializeField] private GameObject keyPrefab;

    private RoomBoundaryGenerator boundaryGenerator;

    private RoomState currentState = RoomState.Unentered;

    // Tracks every enemy spawned into this room that RoomDirector still considers "alive"
    // Populated via RegisterSpawnedEnemy() (called by RoomBoundaryGenerator right after
    // Instantiate()), and shrunk via NotifyEnemyDefeated() (called by EnemyController.Die())
    // Using a list (not a single reference) to support multi-enemy rooms later,
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();

    private GameObject spawnedKeyInstance;

    public RoomState CurrentState => currentState;

    // public accessor for external systems (or future RoomBoundaryGenerator calls) that
    // need to know whether this room still has a live, tracked enemy. Backed by the same
    // activeEnemies list already maintained via RegisterSpawnedEnemy()/NotifyEnemyDefeated(),
    // so this is always in sync with the room's actual clear state 
    public bool HasActiveEnemies => activeEnemies.Count > 0;

    private void Awake()
    {
        boundaryGenerator = GetComponent<RoomBoundaryGenerator>();
    }

    // Called by RoomZoneTrigger (via a small additive hook) when the player crosses INTO
    // this room through one of its child edges. Only the Unentered -> Sealed transition is
    // valid here; re-triggering on an already-sealed/cleared/opened room is a deliberate
    // no-operation, since sealing should only ever happen once per room visit
    public void NotifyPlayerEntered()
    {
        if(currentState != RoomState.Unentered)
        {
            return;
        }

        SealRoom();
    }

    // Called by RoomBoundaryGenerator.SpawnEnemyIfConfigured() right after an enemy is
    // instantiated and initialized for this room
    public void RegisterSpawnedEnemy(EnemyController enemy)
    {
        if(enemy == null)
        {
            return;
        }

        activeEnemies.Add(enemy);
    }

    // Called by EnemyController.Die() (via hook in the Die()) the
    // moment an enemy in this room dies. Checks whether that was the LAST tracked enemy, and
    // if so, transitions Sealed -> Cleared
    public void NotifyEnemyDefeated(EnemyController enemy)
    {
        activeEnemies.Remove(enemy);

        if(currentState != RoomState.Sealed)
        {
            return;
        }

        if(activeEnemies.Count == 0)
        {
            ClearRoom();
        }
    }

    // Called by KeyPickup once the player has collected the spawned key. Only valid from
    // Cleared - if this fires unexpectedly from another state, it's logged and ignored
    // rather than silently corrupting room state
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

    private void ClearRoom()
    {
        currentState = RoomState.Cleared;

        SpawnKey();
    }

    private void OpenRoom()
    {
        currentState = RoomState.Opened;

        boundaryGenerator.RestoreAllEdgeCollisionSettings();
        boundaryGenerator.SetParentEdgeBlocksPlayer(false);
        boundaryGenerator.PaintTraversalEdgesOpen();
    }

    private void SpawnKey()
    {
        if(keyPrefab == null)
        {
            Debug.LogWarning(
                name + ": room cleared but no keyPrefab assigned on RoomDirector - " +
                "the room will remain sealed indefinitely since nothing can trigger " +
                "NotifyKeyCollected().",
                this
            );

            return;
        }

        if(!boundaryGenerator.TryGetKeySpawnWorldPoint(out Vector2 spawnPoint))
        {
            Debug.LogWarning(
                name + ": could not find a valid key spawn point - falling back to this " +
                "GameObject's own position.",
                this
            );

            spawnPoint = transform.position;
        }

        // parent the spawned key under this room's own transform, so:
        //1. it moves/hides correctly alongside the room if the room is ever repositioned or toggled inactive 
        //2. KeyPickup's GetComponentInParent<RoomDirector>() fallback (used if Initialize() were ever skipped) actually has a RoomDirector to find
        spawnedKeyInstance = Instantiate(
            keyPrefab,
            spawnPoint,
            Quaternion.identity,
            transform
        );

        // explicitly wire the KeyPickup -> RoomDirector reference, rather than relying
        // solely on the GetComponentInParent fallback inside KeyPickup.Start()
        KeyPickup keyPickup = spawnedKeyInstance.GetComponent<KeyPickup>();

        if(keyPickup != null)
        {
            keyPickup.Initialize(this);
        }
        else
        {
            Debug.LogWarning(
                name + ": spawned key prefab \"" + keyPrefab.name +
                "\" has no KeyPickup component - it will never notify this RoomDirector " +
                "when collected, and the room will remain sealed indefinitely.",
                this
            );
        }
    }
}