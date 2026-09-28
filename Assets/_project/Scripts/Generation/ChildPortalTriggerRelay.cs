using UnityEngine;

// ChildPortalTriggerRelay - lives on the small circular "ChildPortal" GameObject spawned at the
// midpoint of each child edge (see RoomBoundaryGenerator.CreateChildPortal()).
// To forward this portal's own OnTriggerEnter2D event up to the parent edge's existing
// RoomZoneTrigger component
public class ChildPortalTriggerRelay : MonoBehaviour
{
    private RoomZoneTrigger targetTrigger;

    // Called once by RoomBoundaryGenerator.CreateChildPortal() immediately after this component
    // is added, wiring it to the specific RoomZoneTrigger instance on the parent edge this
    // portal belongs to
    public void Initialize(RoomZoneTrigger trigger)
    {
        targetTrigger = trigger;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(targetTrigger == null)
        {
            Debug.LogWarning(
                name + ": ChildPortalTriggerRelay fired but has no targetTrigger assigned - " +
                "Initialize() was never called. This portal will never trigger traversal.",
                this
            );

            return;
        }

        targetTrigger.HandleExternalTriggerEnter(other);
    }
}