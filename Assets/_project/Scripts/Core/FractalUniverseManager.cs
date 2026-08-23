using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class FractalUniverseManager : MonoBehaviour
{
    [Header("Root Visual")]
    [SerializeField] private GameObject rootVisual;
    [SerializeField] private KochRoomVisibility rootVisibility;

    [Header("Root Motif Layout")]
    [SerializeField] private RootMotifLayout rootMotifLayout;

    [SerializeField] private KochMotifNode rootMotifNode;

    [Header("Room Prefabs (ordered A, F, E, D, C, B)")]
    [Tooltip("One prefab per letter, matching FractalNode.LetterNames order. " +
             "Each prefab must have KochMotifNode + KochMotifAligner + KochRoomVisibility + RoomBoundaryGenerator.")]
    [SerializeField] private GameObject[] letterPrefabs = new GameObject[6];

    [Header("Traversal State")]
    [SerializeField] private TraversalState currentState = TraversalState.Exploration;

    // Replaces path (letters only) + the odd/even bank model 
    private readonly List<KochMotifNode> activeChain = new List<KochMotifNode>();

    private KochRoomVisibility activeRoomVisibility;

    // temp vars to stop multiple edge collisions between room transitions
    [Header("Traversal Protection")]
    [SerializeField] private float traversalCooldown = 0.2f;
    private float traversalBlockedUntil;

    [Header("Zoom Transition In (Root -> Child)")]
    [SerializeField] private float zoomInDuration = 0.8f;
    [SerializeField] private AnimationCurve zoomInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Zoom Transition Out (Child -> Parent)")]
    [SerializeField] private float zoomOutDuration = 0.6f;
    [SerializeField] private AnimationCurve zoomOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public TraversalState CurrentState
    {
        get
        {
            return currentState;
        }
    }
    public int CurrentLevel
    {
        get
        {
            return activeChain.Count;
        }
    }

    public int CurrentLetterIndex
    {
        get
        {
            if(activeChain.Count == 0)
                return -1;

            return activeChain[activeChain.Count - 1].RoomLetterIndex;
        }
    }

    public bool IsAtRoot
    {
        get
        {
            return activeChain.Count == 0;
        }
    }

    public bool CanTraverse
    {
        get
        {
            return currentState == TraversalState.Exploration
                && Time.unscaledTime >= traversalBlockedUntil;
        }
    }

    private void Start()
    {
        ValidateSetup();
        ResetToRoot();
    }

    // Public func request move from current node to specified child letter, if traversal allowed
    public void RequestTraverseToChild(int targetLetterIndex)
    {
        if(!CanTraverse)
            return;

        if(!IsValidLetterIndex(targetLetterIndex))
            return;

        if(IsAtRoot)
        {
            if(!IsValidRootChild(targetLetterIndex))
                return;
        }
        else
        {
            if(!IsValidChildOfCurrentNode(targetLetterIndex))
                return;
        }

        BeginTransitionToChild(targetLetterIndex);
    }

    // Public func request move from current node back to parent, if possible
    public void RequestTraverseToParent()
    {
        if(!CanTraverse)
            return;

        if(IsAtRoot)
        {
            Debug.Log("Parent traversal ignored because the player is at the root.", this);
            return;
        }

        BeginTransitionToParent();
    }

    // Helper - the node currently occupied (root if chain is empty)
    private KochMotifNode CurrentNode
    {
        get
        {
            return activeChain.Count == 0
                ? rootMotifNode
                : activeChain[activeChain.Count - 1];
        }
    }

    private void BeginTransitionToChild(int targetLetterIndex)
    {
        currentState = TraversalState.Transitioning;
        StartCoroutine(ZoomIntoChildCoroutine(targetLetterIndex));
    }

    private void BeginTransitionToParent()
    {
        currentState = TraversalState.Transitioning;
        StartCoroutine(ZoomOutToParentCoroutine());
    }

    // Root obj indexes children by letter (0-5)
    // Room nodes index children by slot (0=prev,1=self,2=next) 
    private int GetSlotForTarget(KochMotifNode parentNode, int targetLetterIndex)
    {
        if(parentNode.IsRootNode)
            return targetLetterIndex;

        return GetChildSlotForLetter(parentNode, targetLetterIndex);
    }

    // Helper - given a room-mode parent node, find which child slot (0/1/2) a target letter
    // occupies. Returns -1 if not found
    // Root mode returns letter index directly
    private int GetChildSlotForLetter(KochMotifNode parentNode, int targetLetterIndex)
    {
        if(parentNode.IsRootNode)
            return targetLetterIndex;

        int[] childLetters = FractalNode.GetChildLetters(parentNode.RoomLetterIndex);

        for(int slot = 0; slot < childLetters.Length; slot++)
        {
            if(childLetters[slot] == targetLetterIndex)
                return slot;
        }

        return -1;
    }

    private GameObject GetPrefabForLetter(int letterIndex)
    {
        if(letterIndex < 0 || letterIndex >= letterPrefabs.Length)
        {
            Debug.LogError("Invalid letter index for prefab lookup: " + letterIndex, this);
            return null;
        }

        return letterPrefabs[letterIndex];
    }

    // instantiate-or-reuse gameobjects
    // used for root's and every rooms' children    
    private KochMotifNode GetOrCreateChild(KochMotifNode parentNode, int slot, int targetLetterIndex)
    {
        KochMotifNode existing = parentNode.GetChildNode(slot);

        if(existing != null)
            return existing;

        GameObject prefab = GetPrefabForLetter(targetLetterIndex);

        if(prefab == null)
            return null;

        GameObject instance = Instantiate(prefab);
        instance.name = "Room_" + FractalNode.LetterNames[targetLetterIndex] + "_depth" + (activeChain.Count + 1);

        KochMotifNode childNode = instance.GetComponent<KochMotifNode>();

        if(childNode == null)
        {
            Debug.LogError(
                "Instantiated prefab for letter " + FractalNode.LetterNames[targetLetterIndex] +
                " has no KochMotifNode component.",
                instance
            );

            return null;
        }

        WireRoomZoneTriggers(childNode);
        parentNode.SetChildNode(slot, childNode);

        return childNode;
    }

    // Func to ensure prefabs instantiated with scene-specific wiring, mirror bank room approach
    // As RoomZoneTrigger.universeManager can no longer do this ahead of time
    private void WireRoomZoneTriggers(KochMotifNode childNode)
    {
        RoomBoundaryGenerator boundaryGenerator = childNode.GetComponentInChildren<RoomBoundaryGenerator>();

        if(boundaryGenerator != null)
        {
            boundaryGenerator.Initialize(this);
        }
        else
        {
            Debug.LogError(
                "WireRoomZoneTriggers: " + childNode.name + " has no RoomBoundaryGenerator.",
                childNode
            );
        }
    }

    private Vector3 GetRoomNormalLocalScale(KochMotifNode node)
    {
        RoomKochLayoutSettings settings = node.GetComponentInChildren<RoomKochLayoutSettings>();

        if(settings != null)
            return settings.normalLocalScale;

        return node.transform.localScale;
    }

    private IEnumerator ZoomIntoChildCoroutine(int targetLetterIndex)
    {
        KochMotifNode parentNode = CurrentNode;
        int slot = GetSlotForTarget(parentNode, targetLetterIndex);

        if(slot < 0)
        {
            Debug.LogError(
                "ZoomIntoChildCoroutine: letter " + FractalNode.LetterNames[targetLetterIndex] +
                " is not a valid child of the current node.",
                this
            );

            currentState = TraversalState.Exploration;
            yield break;
        }

        KochMotifNode childNode = GetOrCreateChild(parentNode, slot, targetLetterIndex);

        if(childNode == null)
        {
            currentState = TraversalState.Exploration;
            yield break;
        }

        // capture normal (fully zoomed-in) scale BEFORE AttachChild changes localScale
        Vector3 normalLocalScale = GetRoomNormalLocalScale(childNode);

        // AttachChild performs the real SetParent + sets localPosition/localRotation/localScale
        // to the correct fitted-into-motif values in one call
        parentNode.AttachChild(childNode, slot);

        Vector3 fittedLocalScale = childNode.transform.localScale;

        KochRoomVisibility incomingVisibility = childNode.GetComponent<KochRoomVisibility>();

        if(incomingVisibility != null)
            incomingVisibility.SetVisualVisible(true);

        Transform roomTransform = childNode.transform;
        roomTransform.localScale = fittedLocalScale;

        float elapsed = 0f;

        while(elapsed < zoomInDuration)
        {
            float t = elapsed / zoomInDuration;
            float eased = zoomInCurve.Evaluate(t);

            roomTransform.localScale = Vector3.LerpUnclamped(fittedLocalScale, normalLocalScale, eased);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        roomTransform.localScale = normalLocalScale;

        // hide whatever was active before (or root, if this is the first hop)
        if(activeRoomVisibility != null)
            activeRoomVisibility.SetVisualVisible(false);
        else
            SetRootVisible(false);

        activeChain.Add(childNode);
        activeRoomVisibility = incomingVisibility;

        traversalBlockedUntil = Time.unscaledTime + traversalCooldown;
        currentState = TraversalState.Exploration;

        Debug.Log(
            "Traversal complete. Level: " + CurrentLevel +
            ", Letter: " + FractalNode.LetterNames[CurrentLetterIndex],
            this
        );
    }

    private IEnumerator ZoomOutToParentCoroutine()
    {
        if(activeChain.Count == 0)
        {
            currentState = TraversalState.Exploration;
            yield break;
        }

        KochMotifNode currentRoomNode = activeChain[activeChain.Count - 1];

        // parent is the previous entry in the chain, or root if we're at depth 1
        KochMotifNode parentNode = activeChain.Count == 1
            ? rootMotifNode
            : activeChain[activeChain.Count - 2];

        Transform roomTransform = currentRoomNode.transform;
        Vector3 normalLocalScale = GetRoomNormalLocalScale(currentRoomNode);
        Vector3 fittedLocalScale = normalLocalScale;

        int slot = GetChildSlotForLetter(parentNode, currentRoomNode.RoomLetterIndex);

        if(slot >= 0 &&
            parentNode.TryGetChildFittedLocalScale(currentRoomNode, slot, out float requiredScale))
        {
            fittedLocalScale = Vector3.one * requiredScale;
        }
        else
        {
            Debug.LogWarning(
                "ZoomOutToParentCoroutine: could not re-derive fitted scale for " +
                currentRoomNode.name +
                " - falling back to normalLocalScale (no shrink animation will be visible).",
                this
            );
        }

        roomTransform.localScale = normalLocalScale;

        float elapsed = 0f;

        while(elapsed < zoomOutDuration)
        {
            float t = elapsed / zoomOutDuration;
            float eased = zoomOutCurve.Evaluate(t);

            roomTransform.localScale = Vector3.LerpUnclamped(normalLocalScale, fittedLocalScale, eased);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        roomTransform.localScale = fittedLocalScale;

        KochRoomVisibility leavingVisibility = currentRoomNode.GetComponent<KochRoomVisibility>();

        if(leavingVisibility != null)
            leavingVisibility.SetVisualVisible(false);

        // Instance NOT destroyed - stays parented/cached in parentNode's child slot for reuse, just hidden
        activeChain.RemoveAt(activeChain.Count - 1);

        if(activeChain.Count == 0)
        {
            SetRootVisible(true);
            activeRoomVisibility = null;
        }
        else
        {
            KochMotifNode newActive = activeChain[activeChain.Count - 1];
            KochRoomVisibility newVisibility = newActive.GetComponent<KochRoomVisibility>();

            if(newVisibility != null)
                newVisibility.SetVisualVisible(true);

            activeRoomVisibility = newVisibility;
        }

        traversalBlockedUntil = Time.unscaledTime + traversalCooldown;
        currentState = TraversalState.Exploration;

        Debug.Log(
            "Traversal complete. Level: " + CurrentLevel +
            ", Letter: " + (IsAtRoot ? "Root" : FractalNode.LetterNames[CurrentLetterIndex]),
            this
        );
    }

    private void SetRootVisible(bool isVisible)
    {
        if(rootVisibility != null)
        {
            rootVisibility.SetVisualVisible(isVisible);
            return;
        }

        if(rootVisual != null)
            rootVisual.SetActive(isVisible);
    }

    private void ResetToRoot()
    {
        activeChain.Clear();
        activeRoomVisibility = null;
        SetRootVisible(true);
        currentState = TraversalState.Exploration;
    }

    private bool IsValidRootChild(int targetLetterIndex)
    {
        return targetLetterIndex >= 0 && targetLetterIndex < FractalNode.LetterNames.Length;
    }

    private bool IsValidChildOfCurrentNode(int targetLetterIndex)
    {
        if(activeChain.Count == 0)
            return false;

        int currentLetterIndex = CurrentLetterIndex;
        int[] validChildren = FractalNode.GetChildLetters(currentLetterIndex);

        foreach(int childLetterIndex in validChildren)
        {
            if(childLetterIndex == targetLetterIndex)
                return true;
        }

        return false;
    }

    private bool IsValidLetterIndex(int letterIndex)
    {
        return letterIndex >= 0 && letterIndex < FractalNode.LetterNames.Length;
    }

    private void ValidateSetup()
    {
        if(letterPrefabs == null || letterPrefabs.Length != FractalNode.LetterNames.Length)
        {
            Debug.LogError(
                "letterPrefabs must contain exactly " + FractalNode.LetterNames.Length + " entries.",
                this
            );
        }
        else
        {
            for(int i = 0; i < letterPrefabs.Length; i++)
            {
                if(letterPrefabs[i] == null)
                {
                    Debug.LogWarning(
                        "letterPrefabs has no prefab assigned for index " + i +
                        " (" + FractalNode.LetterNames[i] + ").",
                        this
                    );

                    continue;
                }

                if(letterPrefabs[i].GetComponent<KochMotifNode>() == null)
                {
                    Debug.LogWarning(
                        "letterPrefabs[" + i + "] (" + FractalNode.LetterNames[i] +
                        ") has no KochMotifNode component.",
                        this
                    );
                }
            }
        }

        if(rootVisual == null)
            Debug.LogWarning("No root visual has been assigned.", this);

        if(rootVisibility == null)
        {
            Debug.LogWarning(
                "No RootVisibility assigned - falling back to GameObject.SetActive on rootVisual.",
                this
            );
        }

        if(rootMotifLayout == null)
            Debug.LogWarning("RootMotifLayout has not been assigned.", this);

        if(rootMotifNode == null)
        {
            Debug.LogWarning(
                "RootMotifNode has not been assigned - traversal will fail. " +
                "Assign the root's KochMotifNode component.",
                this
            );
        }
    }
}
