using UnityEngine;

// KochMotifNode is "real parent/child references" that lives on the root snowflake GameObject AND  every room GameObject//
// Constraint: a GameObject can only have ONE parent in Unity's transform hierarchy at any given moment.
//Holds STATIC references to "which GameObject is which letter, in the opposite bank"
//exposes AttachChild DetachFromParent methods that perform the Unity reparent & local transform alignment dynamically, at moment of traversal
[RequireComponent(typeof(KochMotifAligner))]
public class KochMotifNode : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool isRootNode = false;

    [Header("Room Identity (room nodes only)")]
    [SerializeField] private int roomLetterIndex = 0;

    [Header("Root Mode Children (6, one per letter A,F,E,D,C,B)")]
    [SerializeField] private KochMotifNode[] rootChildNodes = new KochMotifNode[6];

    [Header("Room Mode Children (3 - previous, self, next)")]
    [SerializeField] private KochMotifNode[] roomChildNodes = new KochMotifNode[3];

    private KochMotifAligner aligner;

    // The node this node is CURRENTLY attached under, at runtime. Null when at root or detached.
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

    // Exposes this node's own KochMotifAligner so callers (e.g. a parent's AttachChild call
    // acting on THIS node as the child) can read its OwnRadius when converting a target radius
    // into a correctly-scaled local scale
    public KochMotifAligner Aligner
    {
        get
        {
            if(aligner == null)
            {
                aligner = GetComponent<KochMotifAligner>();
            }

            return aligner;
        }
    }

    private void Awake()
    {
        aligner = GetComponent<KochMotifAligner>();
    }

    // Root mode: get the child node for a given letter index (0..5).
    // Room mode: get the child node for a given child slot (0=previous, 1=self, 2=next).
    public KochMotifNode GetChildNode(int letterIndexOrChildSlot)
    {
        if(isRootNode)
        {
            if(letterIndexOrChildSlot < 0 ||
                letterIndexOrChildSlot >= rootChildNodes.Length)
            {
                Debug.LogError(
                    "KochMotifNode (root): invalid letter index " +
                    letterIndexOrChildSlot,
                    this
                );

                return null;
            }

            return rootChildNodes[letterIndexOrChildSlot];
        }

        if(letterIndexOrChildSlot < 0 ||
            letterIndexOrChildSlot >= roomChildNodes.Length)
        {
            Debug.LogError(
                "KochMotifNode (room " +
                FractalNode.LetterNames[roomLetterIndex] +
                "): invalid child slot " +
                letterIndexOrChildSlot,
                this
            );

            return null;
        }

        return roomChildNodes[letterIndexOrChildSlot];
    }

    // Convenience for room mode: resolve a child node directly by its LETTER (not slot index),
    // using FractalNode.GetChildLetters to find which slot that letter occupies for this room.
    public KochMotifNode GetChildNodeForLetter(int childLetterIndex)
    {
        if(isRootNode)
        {
            return GetChildNode(childLetterIndex);
        }

        int[] childLetters =
            FractalNode.GetChildLetters(roomLetterIndex);

        for(int slot = 0; slot < childLetters.Length; slot++)
        {
            if(childLetters[slot] == childLetterIndex)
            {
                return GetChildNode(slot);
            }
        }

        Debug.LogWarning(
            "KochMotifNode (room " +
            FractalNode.LetterNames[roomLetterIndex] +
            "): letter " +
            FractalNode.LetterNames[childLetterIndex] +
            " is not a valid child of this room.",
            this
        );

        return null;
    }

    // Performs the actual Unity re-parent (via transform.SetParent) and local position/scale,
    // so child's world transform is correct per unity's own hierarchy composition.
    // ROTATION FIX: each room's own drawn content (KochSnowflakeMotifRenderer / RoomBoundaryGenerator)
    // Already bakes correct visual orientation for its own letter directly into geometry
    // Applying extra letter-derived rotation here via transform.localRotation double-rotates the room on top of its own already-correct
    // internal geometry, to fix visible extra rotation (exampple: F appearing upside down)
    // SCALE FIX: correct required local scale is targetRadius / child's own radius, calculated using childNode.Aligner.OwnRadius.
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

        // Root-mode parents keep using aligner-derived data.localPosition
        // Room-mode parents use more reliable RoomBoundaryGenerator attach point
        Vector3 resolvedLocalPosition =
            data.localPosition;

        if(!isRootNode)
        {
            RoomBoundaryGenerator boundaryGenerator =
                GetComponentInChildren<RoomBoundaryGenerator>();

            if(boundaryGenerator != null &&
                childSlotOrLetterIndex >= 0 &&
                childSlotOrLetterIndex < boundaryGenerator.ChildEmergeLocalPoints.Length)
            {
                Vector2 attachPoint =
                    boundaryGenerator.ChildEmergeLocalPoints[childSlotOrLetterIndex];

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

        // Room's own internal geometry already accounts for its letter's correct orientation
        // do not rotate it again here
        childNode.transform.localRotation =
            Quaternion.identity;

        childNode.transform.localScale =
            Vector3.one * requiredLocalScale;

        Debug.Log(
    "AttachChild: parent=" + gameObject.name +
    ", parentLossyScale=" + transform.lossyScale +
    ", parentWorldPos=" + transform.position +
    ", child=" + childNode.name +
    ", childLocalPosition=" + resolvedLocalPosition +
    ", childLocalScale=" + childNode.transform.localScale +
    ", childWorldPosAfter=" + childNode.transform.position,
    this
);

        childNode.currentParentNode = this;
    }

    // Calculate what child's "fitted into motif" local scale WOULD be, without actually
    // attaching/moving anything. Used by FractalUniverseManager's zoom-out animation coroutine
    public bool TryGetChildFittedLocalScale(
        KochMotifNode childNode,
        int childSlotOrLetterIndex,
        out float requiredLocalScale)
    {
        requiredLocalScale = 1f;

        if(childNode == null)
        {
            return false;
        }

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
        {
            return false;
        }

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
