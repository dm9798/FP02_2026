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

    [Header("Traversal Safety")]
    [Tooltip("World-space distance the player is nudged into the destination room/root at the end")]
    [SerializeField] private float traversalPushDistance = 0.15f;

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
    // added crossingT and playerTransform so room's parent edge can place player at equivalent position along it scaled up
    // both params optional with safe defaults
    public void RequestTraverseToChild(
        int targetLetterIndex,
        float crossingT = 0.5f,
        Transform playerTransform = null)
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

        BeginTransitionToChild(targetLetterIndex, crossingT, playerTransform);
    }
    
    // Public func request move from current node back to parent, if possible
    public void RequestTraverseToParent(float crossingT = 0.5f, Transform playerTransform = null)
    {
        if(!CanTraverse)
            return;

        if(IsAtRoot)
        {
            Debug.Log("Parent traversal ignored because the player is at the root.", this);
            return;
        }

        BeginTransitionToParent(crossingT, playerTransform);
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

    private void BeginTransitionToChild(int targetLetterIndex, float crossingT, Transform playerTransform)
    {
        currentState = TraversalState.Transitioning;
        StartCoroutine(ZoomIntoChildCoroutine(targetLetterIndex, crossingT, playerTransform));
    }


    private void BeginTransitionToParent(float crossingT, Transform playerTransform)
    {
        currentState = TraversalState.Transitioning;
        StartCoroutine(ZoomOutToParentCoroutine(crossingT, playerTransform));
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
        RoomKochLayoutSettings settings = node.GetComponentInChildren<RoomKochLayoutSettings>(true);

        if(settings != null)
            return settings.normalLocalScale;

        return node.transform.localScale;
    }

    // Coroutine child-edge (room scale/position lerp, collider enabling, visibility toggling, activeChain logging)   
    private IEnumerator ZoomIntoChildCoroutine(
        int targetLetterIndex,
        float crossingT,
        Transform playerTransform)
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

        Vector3 normalLocalScale = GetRoomNormalLocalScale(childNode);

        // Read world pos from settings -> converts into equivalent local pos relative to parentNode        
        Vector3 normalWorldPosition = GetRoomNormalWorldPosition(childNode);
        Vector3 normalLocalPosition = parentNode.transform.InverseTransformPoint(normalWorldPosition);

        // Start point for the scale and position animation
        parentNode.AttachChild(childNode, slot);

        Vector3 fittedLocalScale = childNode.transform.localScale;
        Vector3 fittedLocalPosition = childNode.transform.localPosition;

        RoomBoundaryGenerator childBoundaryGenerator =
            childNode.GetComponentInChildren<RoomBoundaryGenerator>(true);

        if(childBoundaryGenerator != null)
        {
            childBoundaryGenerator.ReapplyChildEdgeCollisionSettings();
        }
        else
        {
            Debug.LogWarning(
                "ZoomIntoChildCoroutine: " + childNode.name +
                " has no RoomBoundaryGenerator - cannot reapply child edge collision settings; " +
                "falling back to enabling all EdgeCollider2D components (old behaviour).",
                this
            );

            EdgeCollider2D[] roomEdgeColliders = childNode.GetComponentsInChildren<EdgeCollider2D>(true);
            foreach(EdgeCollider2D edgeCollider in roomEdgeColliders)
            {
                if(edgeCollider != null)
                {
                    edgeCollider.enabled = true;
                }
            }
        }

        KochRoomVisibility incomingVisibility = childNode.GetComponentInChildren<KochRoomVisibility>(true);

        if(incomingVisibility != null)
            incomingVisibility.SetVisualVisible(true);

        Transform roomTransform = childNode.transform;
        roomTransform.localScale = fittedLocalScale;
        roomTransform.localPosition = fittedLocalPosition;

        bool canDriveCrossingPlayer =
            playerTransform != null && childBoundaryGenerator != null;

        if(playerTransform != null && childBoundaryGenerator == null)
        {
            Debug.LogWarning(
                "ZoomIntoChildCoroutine: " + childNode.name +
                " has no RoomBoundaryGenerator - skipping player-relative position transition.",
                this
            );
        }

        float elapsed = 0f;

        while(elapsed < zoomInDuration)
        {
            float t = elapsed / zoomInDuration;
            float eased = zoomInCurve.Evaluate(t);

            roomTransform.localScale = Vector3.LerpUnclamped(fittedLocalScale, normalLocalScale, eased);
            roomTransform.localPosition = Vector3.LerpUnclamped(fittedLocalPosition, normalLocalPosition, eased);

            // reread new room's (moving/scaling) parent edge world endpoints
            // snap the player onto equivalent point along that edge every frame
            // using same crossingT captured at the moment they originally crossed the old room's child edge
            // Position only -does not touch rotation & scale
            if(canDriveCrossingPlayer)
            {
                Vector2 currentParentEdgeStart = childBoundaryGenerator.GetParentEdgeWorldStart();
                Vector2 currentParentEdgeEnd = childBoundaryGenerator.GetParentEdgeWorldEnd();

                Vector2 targetPlayerPosition = Vector2.LerpUnclamped(
                    currentParentEdgeStart,
                    currentParentEdgeEnd,
                    crossingT
                );

                playerTransform.position = new Vector3(
                    targetPlayerPosition.x,
                    targetPlayerPosition.y,
                    playerTransform.position.z
                );
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        roomTransform.localScale = normalLocalScale;
        roomTransform.localPosition = normalLocalPosition;

        // ensure the player ends up exactly on new room's final parent edge position
        if(canDriveCrossingPlayer)
        {
            Vector2 finalEdgeStart = childBoundaryGenerator.GetParentEdgeWorldStart();
            Vector2 finalEdgeEnd = childBoundaryGenerator.GetParentEdgeWorldEnd();

            Vector2 finalPlayerPosition = Vector2.LerpUnclamped(finalEdgeStart, finalEdgeEnd, crossingT);

            playerTransform.position = PushPointInward(
                finalEdgeStart,
                finalEdgeEnd,
                childNode.transform,
                finalPlayerPosition,
                playerTransform.position.z
            );
        }

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


    private IEnumerator ZoomOutToParentCoroutine(float crossingT, Transform playerTransform)
    {
        if(activeChain.Count == 0)
        {
            currentState = TraversalState.Exploration;
            yield break;
        }

        KochMotifNode currentRoomNode = activeChain[activeChain.Count - 1];

        KochMotifNode parentNode = activeChain.Count == 1
            ? rootMotifNode
            : activeChain[activeChain.Count - 2];

        Transform roomTransform = currentRoomNode.transform;
        Vector3 normalLocalScale = GetRoomNormalLocalScale(currentRoomNode);
        Vector3 fittedLocalScale = normalLocalScale;

        Vector3 normalWorldPosition = GetRoomNormalWorldPosition(currentRoomNode);
        Vector3 normalLocalPosition = currentRoomNode.transform.parent != null
            ? currentRoomNode.transform.parent.InverseTransformPoint(normalWorldPosition)
            : normalWorldPosition;
        Vector3 fittedLocalPosition = normalLocalPosition;

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

        if(slot >= 0 &&
            parentNode.TryGetChildFittedLocalPosition(currentRoomNode, slot, out Vector3 requiredPosition))
        {
            fittedLocalPosition = requiredPosition;
        }
        else
        {
            Debug.LogWarning(
                "ZoomOutToParentCoroutine: could not re-derive fitted position for " +
                currentRoomNode.name +
                " - falling back to normalLocalPosition (no shrink-position animation will be visible).",
                this
            );
        }

        EdgeCollider2D[] roomEdgeColliders = currentRoomNode.GetComponentsInChildren<EdgeCollider2D>(true);
        foreach(EdgeCollider2D edgeCollider in roomEdgeColliders)
        {
            if(edgeCollider != null)
            {
                edgeCollider.enabled = false;
            }
        }

        bool canDriveCrossingPlayer = false;
        Vector2 parentChildEdgeLocalStart = Vector2.zero;
        Vector2 parentChildEdgeLocalEnd = Vector2.zero;
        Vector3 playerStartPosition = Vector3.zero;

        if(parentNode.IsRootNode)
        {
            // Note - root doesn't rotate itself per-letter the way child rooms do
            // no prev/self/next slot at root, and no letterIndex*60 rotation applied
            // player position transform parent-edge case (root and rooms)
            RootKochLayoutSettings rootLayoutSettings =
                parentNode.GetComponentInChildren<RootKochLayoutSettings>(true);

            if(playerTransform != null && rootLayoutSettings != null)
            {
                int letterIndex = currentRoomNode.RoomLetterIndex;

                float angleA = rootLayoutSettings.boundaryAngleOffset + letterIndex * 60f;
                float angleB = rootLayoutSettings.boundaryAngleOffset + ((letterIndex + 1) % 6) * 60f;

                parentChildEdgeLocalStart = rootLayoutSettings.center + rootLayoutSettings.BoundaryRadius * new Vector2(
                    Mathf.Cos(angleA * Mathf.Deg2Rad),
                    Mathf.Sin(angleA * Mathf.Deg2Rad)
                );

                parentChildEdgeLocalEnd = rootLayoutSettings.center + rootLayoutSettings.BoundaryRadius * new Vector2(
                    Mathf.Cos(angleB * Mathf.Deg2Rad),
                    Mathf.Sin(angleB * Mathf.Deg2Rad)
                );

                playerStartPosition = playerTransform.position;
                canDriveCrossingPlayer = true;
            }
            else if(playerTransform != null)
            {
                Debug.LogWarning(
                    "ZoomOutToParentCoroutine: root parent " + parentNode.name +
                    " has no RootKochLayoutSettings - skipping player-relative position transition.",
                    this
                );
            }
        }
        else
        {
            RoomBoundaryGenerator parentBoundaryGenerator =
                parentNode.GetComponentInChildren<RoomBoundaryGenerator>(true);

            if(playerTransform != null && parentBoundaryGenerator != null && slot >= 0)
            {
                RoomKochLayoutSettings parentLayoutSettings =
                    parentNode.GetComponentInChildren<RoomKochLayoutSettings>(true);

                if(parentLayoutSettings != null)
                {
                    int parentRotationSteps = parentNode.RoomLetterIndex;
                    float parentRotationAngle = parentRotationSteps * 60f;

                    Vector2 cornerA = parentLayoutSettings.center + parentLayoutSettings.BoundaryRadius * new Vector2(
                        Mathf.Cos(slot * 60f * Mathf.Deg2Rad),
                        Mathf.Sin(slot * 60f * Mathf.Deg2Rad)
                    );

                    Vector2 cornerB = parentLayoutSettings.center + parentLayoutSettings.BoundaryRadius * new Vector2(
                        Mathf.Cos((slot + 1) * 60f * Mathf.Deg2Rad),
                        Mathf.Sin((slot + 1) * 60f * Mathf.Deg2Rad)
                    );

                    parentChildEdgeLocalStart = RotateAroundCenter(cornerA, parentRotationAngle, parentLayoutSettings.center);
                    parentChildEdgeLocalEnd = RotateAroundCenter(cornerB, parentRotationAngle, parentLayoutSettings.center);

                    playerStartPosition = playerTransform.position;
                    canDriveCrossingPlayer = true;
                }
                else
                {
                    Debug.LogWarning(
                        "ZoomOutToParentCoroutine: parent " + parentNode.name +
                        " has no RoomKochLayoutSettings - skipping player-relative position transition.",
                        this
                    );
                }
            }
            else if(playerTransform != null)
            {
                Debug.LogWarning(
                    "ZoomOutToParentCoroutine: parent " + parentNode.name +
                    " has no RoomBoundaryGenerator - skipping player-relative position transition.",
                    this
                );
            }
        }

        roomTransform.localScale = normalLocalScale;
        roomTransform.localPosition = normalLocalPosition;

        float elapsed = 0f;

        while(elapsed < zoomOutDuration)
        {
            float t = elapsed / zoomOutDuration;
            float eased = zoomOutCurve.Evaluate(t);

            roomTransform.localScale = Vector3.LerpUnclamped(normalLocalScale, fittedLocalScale, eased);
            roomTransform.localPosition = Vector3.LerpUnclamped(normalLocalPosition, fittedLocalPosition, eased);

            if(canDriveCrossingPlayer)
            {
                Vector2 targetEdgeStart = parentNode.transform.TransformPoint(parentChildEdgeLocalStart);
                Vector2 targetEdgeEnd = parentNode.transform.TransformPoint(parentChildEdgeLocalEnd);

                Vector2 targetPlayerPosition = Vector2.LerpUnclamped(
                    targetEdgeStart,
                    targetEdgeEnd,
                    crossingT
                );

                Vector3 targetPlayerPosition3D = new Vector3(
                    targetPlayerPosition.x,
                    targetPlayerPosition.y,
                    playerStartPosition.z
                );

                playerTransform.position = Vector3.LerpUnclamped(
                    playerStartPosition,
                    targetPlayerPosition3D,
                    eased
                );
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        roomTransform.localScale = fittedLocalScale;
        roomTransform.localPosition = fittedLocalPosition;

        if(canDriveCrossingPlayer)
        {
            Vector2 finalEdgeStart = parentNode.transform.TransformPoint(parentChildEdgeLocalStart);
            Vector2 finalEdgeEnd = parentNode.transform.TransformPoint(parentChildEdgeLocalEnd);

            Vector2 finalPlayerPosition = Vector2.LerpUnclamped(finalEdgeStart, finalEdgeEnd, crossingT);

            playerTransform.position = PushPointInward(
                finalEdgeStart,
                finalEdgeEnd,
                parentNode.transform,
                finalPlayerPosition,
                playerStartPosition.z
            );
        }


        KochRoomVisibility leavingVisibility = currentRoomNode.GetComponentInChildren<KochRoomVisibility>(true);

        if(leavingVisibility != null)
            leavingVisibility.SetVisualVisible(false);

        activeChain.RemoveAt(activeChain.Count - 1);

        if(activeChain.Count == 0)
        {
            SetRootVisible(true);
            activeRoomVisibility = null;
        }
        else
        {
            KochMotifNode newActive = activeChain[activeChain.Count - 1];
            KochRoomVisibility newVisibility = newActive.GetComponentInChildren<KochRoomVisibility>(true);

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



    // Helper - return world position from settings.normalWorldPosition
    private Vector3 GetRoomNormalWorldPosition(KochMotifNode node)
    {
        RoomKochLayoutSettings settings = node.GetComponentInChildren<RoomKochLayoutSettings>(true);

        if(settings != null)
            return settings.normalWorldPosition;

        // default to game obj's current world position if no settings found
        return node.transform.position;
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


    // helper - rotate-around-a-center math
    // compromise to mimic RoomBoundaryGenerator hex array logic which is private
    private Vector2 RotateAroundCenter(Vector2 point, float angleDegrees, Vector2 center)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        Vector2 offset = point - center;

        Vector2 rotated = new Vector2(
            offset.x * cos - offset.y * sin,
            offset.x * sin + offset.y * cos
        );

        return center + rotated;
    }

    // helper - point on an edge + edge's two endpoints (all world space)
    // return 1st point + nudged inward into the room/root entered
    // child room - parent room - root cases
    private Vector3 PushPointInward(
        Vector2 edgeWorldStart,
        Vector2 edgeWorldEnd,
        Transform destinationTransform,
        Vector2 pointOnEdge,
        float zPosition)
    {
        Vector2 edgeVector = edgeWorldEnd - edgeWorldStart;
        float edgeLength = edgeVector.magnitude;

        if(edgeLength < 0.0001f || destinationTransform == null)
        {
            return new Vector3(pointOnEdge.x, pointOnEdge.y, zPosition);
        }

        Vector2 edgeDirection = edgeVector / edgeLength;

        // work out which 1 of 2 perpendiculars points toward entered destination
        Vector2 normalA = new Vector2(-edgeDirection.y, edgeDirection.x);
        Vector2 normalB = new Vector2(edgeDirection.y, -edgeDirection.x);

        Vector2 edgeMidpoint = (edgeWorldStart + edgeWorldEnd) * 0.5f;
        Vector2 towardDestination = (Vector2)destinationTransform.position - edgeMidpoint;

        Vector2 inwardNormal = Vector2.Dot(normalA, towardDestination) >= Vector2.Dot(normalB, towardDestination)
            ? normalA
            : normalB;

        Vector2 pushedPoint = pointOnEdge + inwardNormal * traversalPushDistance;

        return new Vector3(pushedPoint.x, pushedPoint.y, zPosition);
    }
}
