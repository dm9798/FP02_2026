
// KeyPickup - must be attached to key prefab that RoomDirector.SpawnKey() instantiates
// Detects the player passing through via a trigger collider, notifies the owning
// RoomDirector that the key has been collected, then removes itself
// Requires a Collider2D on this GameObject (or a child) with "Is Trigger" enabled 
// Requires the player GameObject to be tagged "Player"
// Mirroring  same convention already used by EnemyController.Start()'s GameObject.FindWithTag("Player") fallback

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    //TO BE IMPLEMENTED
    [Header("Collection Feedback")]
    [Tooltip("Optional Animator trigger name to fire on pickup")]
    [SerializeField] private string pickupAnimatorTrigger = "";

    //How long to wait after triggering the pickup animation before actually destroying this GameObject
    [SerializeField] private float destroyDelay = 0f;

    private Animator animatorComponent;
    private Collider2D triggerCollider;
    private bool isCollected;

    // RoomDirector to notify once collected. Assigned by RoomDirector right after
    // Instantiate() in SpawnKey() via Initialize() - mirrors the same
    // "Initialize() before first Update()" pattern already used by EnemyController and
    // RoomBoundaryGenerator elsewhere in this project
    //Fallback including for failure
    private RoomDirector ownerRoomDirector;

    public void Initialize(RoomDirector director)
    {
        ownerRoomDirector = director;
    }

    private void Awake()
    {
        animatorComponent = GetComponent<Animator>();
        triggerCollider = GetComponent<Collider2D>();

        if(triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                name + ": KeyPickup's Collider2D does not have 'Is Trigger' enabled - " +
                "forcing it on, since pickup detection relies on trigger events.",
                this
            );

            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        if(ownerRoomDirector == null)
        {
            ownerRoomDirector = GetComponentInParent<RoomDirector>();
        }

        if(ownerRoomDirector == null)
        {
            Debug.LogWarning(
                name + ": KeyPickup could not resolve a RoomDirector (neither assigned via " +
                "Initialize() nor found in parents) - collecting this key will not unlock " +
                "the room.",
                this
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(isCollected)
        {
            return;
        }

        if(!other.CompareTag(playerTag))
        {
            return;
        }

        Collect();
    }

    private void Collect()
    {
        isCollected = true;

        if(triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        if(ownerRoomDirector != null)
        {
            ownerRoomDirector.NotifyKeyCollected();
        }

        if(animatorComponent != null && !string.IsNullOrEmpty(pickupAnimatorTrigger))
        {
            animatorComponent.SetTrigger(pickupAnimatorTrigger);
        }

        if(destroyDelay > 0f)
        {
            StartCoroutine(DestroyAfterDelay(destroyDelay));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}