using UnityEngine;

// KochMotifNode is "real parent/child references" that lives on the root snowflake gameobj & room gameobj/prefab instances
// Constraint: gameobject can only have ONE parent in transform hierarchy at any given moment
// Holds references to "which instance occupies which child slot", populated LAZILY at runtime
// AttachChild/DetachFromParent methods perform reparent & local transform
// alignment dynamically during traversal
[RequireComponent(typeof(KochMotifAligner))]
public class KochMotifNode : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool isRootNode = false;

    [Header("Room Identity (room nodes only)")]
    [SerializeField] private int roomLetterIndex = 0;

    // Root Mode: 6 slots, one per letter A,F,E,D,C,B - populated lazily as each is first visited
    // Room Mode: 3 slots - previous, self, next - populated lazily as each is first visited
    // NOTE: arrays are still sized appropriately based on isRootNode, but start EMPTY at run
    // FractalUniverseManager method GetOrCreateChild is what populate thems    
    [SerializeField] private KochMotifNode[] rootChildNodes = new KochMotifNode[6];
    [SerializeField] private KochMotifNode[] roomChildNodes = new KochMotifNode[3];

    private KochMotifAligner aligner;

    // The node this node is CURRENTLY attached under, at runtime. Null when rootor detached
    private KochMotifNode currentParentNode;

    public int RoomLetterIndex
    {
        get
        {
            return roomLetterIndex;
        }
    }

    public bool IsRootNode
    {
        get
        {
            return isRootNode;
        }
    }

    public KochMotifNode CurrentParentNode
    {
        get
        {
            return currentParentNode;
        }
    }

    // Exposes this node own KochMotifAligner so callers
    // can read OwnRadius when converting a target radius
    // into correctly-scaled local scale
    public KochMotifAligner Aligner
    {
        get
        {
            if(aligner == null)
                aligner = GetComponent<KochMotifAligner>();

            return aligner;
        }
    }

    private void Awake()
    {
        aligner = GetComponent<KochMotifAligner>();
    }

    // Root mode - get child node for a given letter index (0-5) or null if not yet visited
    // Room mode - get child node for a given child slot (0=previous, 1=self, 2=next) or null
    public KochMotifNode GetChildNode(int letterIndexOrChildSlot)
    {
        if(isRootNode)
        {
            if(letterIndexOrChildSlot < 0 || letterIndexOrChildSlot >= rootChildNodes.Length)
            {
                Debug.LogError(
                    "KochMotifNode (root): invalid letter index " + letterIndexOrChildSlot,
                    this
                );

                return null;
            }

            return rootChildNodes[letterIndexOrChildSlot];
        }

        if(letterIndexOrChildSlot < 0 || letterIndexOrChildSlot >= roomChildNodes.Length)
        {
            Debug.LogError(
                "KochMotifNode (room " + FractalNode.LetterNames[roomLetterIndex] +
                "): invalid child slot " + letterIndexOrChildSlot,
                this
            );

            return null;
        }

        return roomChildNodes[letterIndexOrChildSlot];
    }

    // room mode- resolve a child node directly by its LETTER (not slot index)
    // FractalNode.GetChildLetters to find which slot that letter occupies for room
    public KochMotifNode GetChildNodeForLetter(int childLetterIndex)
    {
        if(isRootNode)
            return GetChildNode(childLetterIndex);

        int[] childLetters = FractalNode.GetChildLetters(roomLetterIndex);

        for(int slot = 0; slot < childLetters.Length; slot++)
        {
            if(childLetters[slot] == childLetterIndex)
                return GetChildNode(slot);
        }

        Debug.LogWarning(
            "KochMotifNode (room " + FractalNode.LetterNames[roomLetterIndex] +
            "): letter " + FractalNode.LetterNames[childLetterIndex] +
            " is not a valid child of this room.",
            this
        );

        return null;
    }

    // populate a child slot with an instantiated or cached node
    // Via  FractalUniverseManager.GetOrCreateChild() ONLY
    public void SetChildNode(int letterIndexOrChildSlot, KochMotifNode childNode)
    {
        if(isRootNode)
        {
            if(letterIndexOrChildSlot < 0 || letterIndexOrChildSlot >= rootChildNodes.Length)
            {
                Debug.LogError(
                    "KochMotifNode (root): invalid letter index " + letterIndexOrChildSlot +
                    " when setting child node.",
                    this
                );

                return;
            }

            rootChildNodes[letterIndexOrChildSlot] = childNode;
            return;
        }

        if(letterIndexOrChildSlot < 0 || letterIndexOrChildSlot >= roomChildNodes.Length)
        {
            Debug.LogError(
                "KochMotifNode (room " + FractalNode.LetterNames[roomLetterIndex] +
                "): invalid child slot " + letterIndexOrChildSlot +
                " when setting child node.",
                this
            );

            return;
        }

        roomChildNodes[letterIndexOrChildSlot] = childNode;
    }

    // Performs the actual Unity re-parent (via transform.SetParent) and local position/scale
    // so child's world transform is correct per unity's own hierarchy in-built rules   
    public void AttachChild(
      KochMotifNode childNode,
      int childSlotOrLetterIndex)
    {
        if(childNode == null)
        {
            Debug.LogError(
                "KochMotifNode.AttachChild: childNode is null.",
                this
            );

            return;
        }

        if(!Aligner.TryGetChildLocalTransform(
                childSlotOrLetterIndex,
                out KochMotifAligner.ChildTransformData data))
        {
            Debug.LogError(
                "KochMotifNode.AttachChild: failed to resolve child transform data " +
                "for slot/letter " +
                childSlotOrLetterIndex,
                this
            );

            return;
        }

        float childOwnRadius =
            childNode.Aligner != null
                ? childNode.Aligner.OwnRadius
                : 0f;

        if(childOwnRadius <= 0f)
        {
            Debug.LogError(
                "KochMotifNode.AttachChild: child " +
                childNode.name +
                " has an invalid or missing OwnRadius on its KochMotifAligner - " +
                "cannot compute a correct scale.",
                this
            );

            return;
        }

        float requiredLocalScale =
            data.targetRadius / childOwnRadius;

        // Root-mode parents keep using aligner-derived data.localPosition.
        // Room-mode parents use the more reliable RoomBoundaryGenerator attach point 
        Vector3 resolvedLocalPosition =
            data.localPosition;

        if(!isRootNode)
        {
            RoomBoundaryGenerator boundaryGenerator = GetComponentInChildren<RoomBoundaryGenerator>(true);

            if(boundaryGenerator != null &&
                childSlotOrLetterIndex >= 0 &&
                childSlotOrLetterIndex < boundaryGenerator.ChildEmergeLocalPoints.Length)
            {
                Vector2 attachPoint =
                    boundaryGenerator.ChildEmergeLocalPoints[childSlotOrLetterIndex];

                // Check due to previous bug which would silently place child at this node's own centre instead of failing in a hard to detect way
                // Flag rather than pass silently as mistaken for several other causes before real bug was found
                if(attachPoint == Vector2.zero)
                {
                    Debug.LogWarning(
                        gameObject.name + "'s ChildEmergeLocalPoints[" + childSlotOrLetterIndex +
                        "] is exactly (0,0) - possibly uninitialized " +
                        "(RoomBoundaryGenerator.Initialize() may not have been called yet " +
                        "on this parent before this attach).",
                        this
                    );
                }

                resolvedLocalPosition =
                    new Vector3(attachPoint.x, attachPoint.y, 0f);
            }
            else
            {
                Debug.LogWarning(
                    "KochMotifNode.AttachChild: " +
                    gameObject.name +
                    " has no RoomBoundaryGenerator (or invalid slot) - " +
                    "falling back to KochMotifAligner-derived position.",
                    this
                );
            }
        }

        childNode.transform.SetParent(
            transform,
            worldPositionStays: false
        );

        childNode.transform.localPosition = resolvedLocalPosition;

        // Room's own geometry already accounts for its correct orientation (per its letter config)
        // do not rotate it again
        childNode.transform.localRotation =
            Quaternion.identity;

        childNode.transform.localScale =
            Vector3.one * requiredLocalScale;

        childNode.currentParentNode = this;
    }

    // Calculate what child's "fitted into motif" local scale WOULD be, without actually
    // attaching/moving anything. Used by FractalUniverseManager's zoom-out animation coroutine.
    public bool TryGetChildFittedLocalScale(
        KochMotifNode childNode,
        int childSlotOrLetterIndex,
        out float requiredLocalScale)
    {
        requiredLocalScale = 1f;

        if(childNode == null)
            return false;

        if(!Aligner.TryGetChildLocalTransform(
                childSlotOrLetterIndex,
                out KochMotifAligner.ChildTransformData data))
        {
            return false;
        }

        float childOwnRadius =
            childNode.Aligner != null
                ? childNode.Aligner.OwnRadius
                : 0f;

        if(childOwnRadius <= 0f)
            return false;

        requiredLocalScale =
            data.targetRadius / childOwnRadius;

        return true;
    }

    // Detaches this node from whatever it is currently parented under  
    public void DetachFromParent()
    {
        transform.SetParent(
            null,
            worldPositionStays: true
        );

        currentParentNode = null;
    }
}
