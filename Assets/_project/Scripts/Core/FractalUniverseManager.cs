using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class FractalUniverseManager : MonoBehaviour
{
    //To differentiate koch snowflake gameobjects in hierarchy
    private enum VisualBank
    {
        None,
        Odd,
        Even
    }

    //root fractal 
    [Header("Root Visual")]
    [SerializeField] private GameObject rootVisual;

    //Root's own visibility toggler. Assign the KochRoomVisibility component on root
    [SerializeField] private KochRoomVisibility rootVisibility;

    [Header("Root Motif Layout")]
    [SerializeField] private RootMotifLayout rootMotifLayout;

    //Direct reference to root's KochMotifNode, which holds the authoritative parent/child GameObject wiring (root -> 6 odd-bank rooms)
    //performs the actual Unity reparent and local-transform alignment via KochMotifAligner
    [SerializeField] private KochMotifNode rootMotifNode;

    [Header("Odd Room Bank")]
    [Tooltip(
        "Room objects ordered as A, F, E, D, C, B " +
        "to match FractalNode.LetterNames."
    )]
    [SerializeField]
    private GameObject[] oddRoomObjects =
        new GameObject[6];

    [Header("Even Room Bank")]
    [Tooltip(
        "Room objects ordered as A, F, E, D, C, B " +
        "to match FractalNode.LetterNames."
    )]
    [SerializeField]
    private GameObject[] evenRoomObjects =
        new GameObject[6];



    [Header("Traversal State")]
    [SerializeField]
    private TraversalState currentState =
        TraversalState.Exploration;

    //keep log of fractal room transitions
    //for debugging
    private readonly List<int> path =
        new List<int>();

    private VisualBank activeRoomBank =
        VisualBank.None;

    //tracks the KochMotifNode of whichever room is currently active
    private KochMotifNode activeRoomNode;

    //tracks the KochRoomVisibility of whichever room is currently visible
    private KochRoomVisibility activeRoomVisibility;

    //temp vars to stop multiple edge collisions between room transitions
    //to be removed following zoom implementation
    [Header("Traversal Protection")]
    [SerializeField] private float traversalCooldown = 0.2f;

    private float traversalBlockedUntil;

    //[Header("Zoom Transition")]
    //[SerializeField] private float zoomDuration = 0.8f;
    //[SerializeField] private AnimationCurve zoomScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Header("Zoom Transition In (Root -> Child)")]
    [SerializeField] private float zoomInDuration = 0.8f;
    [SerializeField] private AnimationCurve zoomInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Header("Zoom Transition Out (Child -> Parent)")]
    [SerializeField] private float zoomOutDuration = 0.6f;
    [SerializeField] private AnimationCurve zoomOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    //getters for traversal logic
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
            return path.Count;
        }
    }

    public int CurrentLetterIndex
    {
        get
        {
            if(path.Count == 0)
                return -1;

            return path[path.Count - 1];
        }
    }

    public IReadOnlyList<int> CurrentPath
    {
        get
        {
            return path;
        }
    }

    public bool IsAtRoot
    {
        get
        {
            return path.Count == 0;
        }
    }


    public bool CanTraverse
    {
        get
        {
            //temporary fix until transition is working
            //to bypass multiple trigger collisions error
            return currentState ==
                TraversalState.Exploration
                &&
                Time.unscaledTime >=
                traversalBlockedUntil;
        }
    }

    private void Start()
    {
        ValidateSetup();
        ResetToRoot();
    }

    //Public func request move from current node to specified child letter, if traversal allowed
    public void RequestTraverseToChild(
        int targetLetterIndex)


    {

        Debug.Log(
    "RequestTraverseToChild: targetLetterIndex=" + targetLetterIndex +
    ", IsAtRoot=" + IsAtRoot +
    ", CurrentPath=" + GetPathName(),
    this
        );

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
            if(!IsValidChildOfCurrentNode(
                targetLetterIndex))
            {
                return;
            }
        }

        List<int> targetPath =
            new List<int>(path);

        targetPath.Add(targetLetterIndex);

        BeginTransitionToPath(targetPath);
    }

    //Public func request move from current node back to parent, if possible
    public void RequestTraverseToParent()
    {
        if(!CanTraverse)
            return;

        if(IsAtRoot)
        {
            Debug.Log(
                "Parent traversal ignored because " +
                "the player is at the root.",
                this
            );

            return;
        }

        List<int> targetPath =
            new List<int>(path);

        targetPath.RemoveAt(
            targetPath.Count - 1
        );

        BeginTransitionToPath(targetPath);
    }

    // Starts transition to target path - select incoming roombank, prep visuals
    private void BeginTransitionToPath(
        List<int> targetPath)
    {
        Debug.Log(
    "BeginTransitionToPath: currentPath=" + GetPathName() +
    ", targetPath length=" + targetPath.Count,
    this
);

        currentState =
            TraversalState.Transitioning;

        bool isChildTransition =
            targetPath.Count >
            path.Count; // going deeper into the tree

        if(isChildTransition)
        {
            VisualBank incomingBank =
                GetIncomingBank();

            PrepareIncomingVisual(
                targetPath,
                incomingBank
            );

            StartCoroutine(
                ZoomIntoChildCoroutine(
                    targetPath,
                    incomingBank
                )
            );
        }
        else
        {         
            StartCoroutine(
                ZoomOutToParentCoroutine(
                    targetPath,
                    activeRoomBank
                )
            );
        }
    }


    //Set up incoming visual (root/room) - hides all OTHER rooms' visuals in the incoming bank    
    private void PrepareIncomingVisual(
        List<int> targetPath,
        VisualBank incomingBank)
    {
        if(targetPath.Count == 0)
        {
            SetBankVisualsVisible(incomingBank, false);

            SetRootVisible(true);

            return;
        }

        int targetLetterIndex =
            targetPath[targetPath.Count - 1];

        GameObject incomingRoom =
            GetRoomObject(
                incomingBank,
                targetLetterIndex
            );

        if(incomingRoom == null)
        {
            Debug.LogError(
                "No room object assigned for letter index " +
                targetLetterIndex +
                " in the " +
                incomingBank +
                " bank.",
                this
            );

            return;
        }

        // Hide every OTHER room's visual in the incoming bank 
        SetBankVisualsVisible(
            incomingBank,
            false
        );

        KochRoomVisibility incomingVisibility =
            incomingRoom.GetComponent<KochRoomVisibility>();

        if(incomingVisibility != null)
        {
            incomingVisibility.SetVisualVisible(true);
        }
        else
        {
            Debug.LogWarning(
                "PrepareIncomingVisual: " +
                incomingRoom.name +
                " has no KochRoomVisibility component.",
                incomingRoom
            );
        }
    }

    //Finalize traversal - hide the room we just left
    //show new visual, update path & active bank/node/visibility tracking
    private void CompleteTransition(
        List<int> targetPath,
        VisualBank incomingBank)
    {
        if(activeRoomVisibility != null)
        {
            activeRoomVisibility.SetVisualVisible(false);
        }

        if(targetPath.Count == 0)
        {
            SetBankVisualsVisible(
                incomingBank,
                false
            );

            SetRootVisible(true);

            activeRoomBank =
                VisualBank.None;

            activeRoomNode =
                null;

            activeRoomVisibility =
                null;
        }
        else
        {
            SetRootVisible(false);

            int targetLetterIndex =
                targetPath[targetPath.Count - 1];

            GameObject targetRoom =
                GetRoomObject(
                    incomingBank,
                    targetLetterIndex
                );

            if(targetRoom != null)
            {
                KochRoomVisibility targetVisibility =
                    targetRoom.GetComponent<KochRoomVisibility>();

                if(targetVisibility != null)
                {
                    targetVisibility.SetVisualVisible(true);
                }

                activeRoomNode =
                    targetRoom.GetComponent<KochMotifNode>();

                activeRoomVisibility =
                    targetVisibility;
            }

            activeRoomBank =
                incomingBank;
        }

        path.Clear();
        path.AddRange(targetPath);

        //true game engine time!
        traversalBlockedUntil =
    Time.unscaledTime + traversalCooldown;

        currentState =
            TraversalState.Exploration;

        Debug.Log(
            "Traversal complete. " +
            "Level: " +
            CurrentLevel +
            ", Letter: " +
            GetCurrentLetterName() +
            ", Path: " +
            GetPathName(),
            this
        );
    }

    //Helper - return which room bank (odd/even) should be used next based on current active one
    private VisualBank GetIncomingBank()
    {
        if(activeRoomBank == VisualBank.Odd)
            return VisualBank.Even;

        return VisualBank.Odd;
    }

    //Helper - retrieve room game obj for given bank & letter index, null if invalid
    private GameObject GetRoomObject(
        VisualBank bank,
        int letterIndex)
    {
        if(!IsValidLetterIndex(letterIndex))
            return null;

        GameObject[] roomObjects;

        if(bank == VisualBank.Odd)
        {
            roomObjects = oddRoomObjects;
        }
        else
        {
            roomObjects = evenRoomObjects;
        }

        if(roomObjects == null)
            return null;

        if(letterIndex >= roomObjects.Length)
            return null;

        return roomObjects[letterIndex];
    }

    //hides/shows every room's VISUAL in the given bank via KochRoomVisibility
    //never touching GameObject.SetActive
    private void SetBankVisualsVisible(
        VisualBank bank,
        bool isVisible)
    {
        GameObject[] roomObjects;

        if(bank == VisualBank.Odd)
        {
            roomObjects = oddRoomObjects;
        }
        else
        {
            roomObjects = evenRoomObjects;
        }

        if(roomObjects == null)
            return;

        foreach(GameObject roomObject in roomObjects)
        {
            if(roomObject == null)
            {
                continue;
            }

            KochRoomVisibility visibility =
                roomObject.GetComponent<KochRoomVisibility>();

            if(visibility != null)
            {
                visibility.SetVisualVisible(isVisible);
            }
        }
    }

    //shows/hides the root's own visual. Uses rootVisibility if assigned
    //otherwise falls back to GameObject.SetActive on rootVisual directly
    private void SetRootVisible(bool isVisible)
    {
        if(rootVisibility != null)
        {
            rootVisibility.SetVisualVisible(isVisible);
            return;
        }

        if(rootVisual != null)
        {
            rootVisual.SetActive(isVisible);
        }
    }

    //Helper - clear traversal path, hides all room visuals, show root visual
    private void ResetToRoot()
    {
        path.Clear();

        SetBankVisualsVisible(
            VisualBank.Odd,
            false
        );

        SetBankVisualsVisible(
            VisualBank.Even,
            false
        );

        SetRootVisible(true);

        activeRoomBank =
            VisualBank.None;

        activeRoomNode =
            null;

        activeRoomVisibility =
            null;

        currentState =
            TraversalState.Exploration;
    }

    //Helper - check whether letter index within range for root-level child
    private bool IsValidRootChild(
        int targetLetterIndex)
    {
        return targetLetterIndex >= 0
            && targetLetterIndex <
            FractalNode.LetterNames.Length;
    }

    //Helper - verify target letter index is valid child of current node
    private bool IsValidChildOfCurrentNode(
        int targetLetterIndex)
    {
        if(path.Count == 0)
            return false;

        int currentLetterIndex =
            path[path.Count - 1];

        int[] validChildren =
            FractalNode.GetChildLetters(
                currentLetterIndex
            );

        foreach(int childLetterIndex in validChildren)
        {
            if(childLetterIndex ==
                targetLetterIndex)
            {
                return true;
            }
        }

        return false;
    }

    //Helper - check letter index within the bounds of FractalNode.LetterNames
    private bool IsValidLetterIndex(
        int letterIndex)
    {
        return letterIndex >= 0
            && letterIndex <
            FractalNode.LetterNames.Length;
    }

    //Helper - return name of current letter or root.
    private string GetCurrentLetterName()
    {
        if(CurrentLetterIndex < 0)
            return "Root";

        return FractalNode.LetterNames[
            CurrentLetterIndex
        ];
    }

    //Helper - string representing traversal path from root to current node
    private string GetPathName()
    {
        if(path.Count == 0)
            return "Root";

        List<string> letters =
            new List<string>();

        foreach(int letterIndex in path)
        {
            letters.Add(
                FractalNode.LetterNames[
                    letterIndex
                ]
            );
        }

        return "Root -> " +
            string.Join(" -> ", letters);
    }

    //Helper - validate room banks
    private void ValidateSetup()
    {
        ValidateRoomBank(
            oddRoomObjects,
            "Odd"
        );

        ValidateRoomBank(
            evenRoomObjects,
            "Even"
        );

        if(rootVisual == null)
        {
            Debug.LogWarning(
                "No root visual has been assigned.",
                this
            );
        }

        if(rootVisibility == null)
        {
            Debug.LogWarning(
                "No RootVisibility assigned - falling back to GameObject.SetActive on " +
                "rootVisual.",
                this
            );
        }

        if(rootMotifLayout == null)
        {
            Debug.LogWarning(
                "RootMotifLayout has not been assigned.",
                this
            );
        }

        if(rootMotifNode == null)
        {
            Debug.LogWarning(
                "RootMotifNode has not been assigned - child transitions will fail. " +
                "Assign the root's KochMotifNode component.",
                this
            );
        }
    }

    //helper - validate selected room bank array of errors
    private void ValidateRoomBank(
        GameObject[] roomObjects,
        string bankName)
    {
        if(roomObjects == null)
        {
            Debug.LogError(
                bankName +
                " room bank is null.",
                this
            );

            return;
        }

        if(roomObjects.Length !=
            FractalNode.LetterNames.Length)
        {
            Debug.LogError(
                bankName +
                " room bank must contain exactly " +
                FractalNode.LetterNames.Length +
                " objects.",
                this
            );
        }

        for(int i = 0; i < roomObjects.Length; i++)
        {
            if(roomObjects[i] == null)
            {
                Debug.LogWarning(
                    bankName +
                    " room bank has no object assigned " +
                    "for index " +
                    i +
                    " (" +
                    GetLetterNameSafely(i) +
                    ").",
                    this
                );

                continue;
            }

            if(roomObjects[i].GetComponent<KochMotifNode>() == null)
            {
                Debug.LogWarning(
                    bankName +
                    " room bank object at index " +
                    i +
                    " (" +
                    GetLetterNameSafely(i) +
                    ") has no KochMotifNode component.",
                    this
                );
            }

            if(roomObjects[i].GetComponent<KochRoomVisibility>() == null)
            {
                Debug.LogWarning(
                    bankName +
                    " room bank object at index " +
                    i +
                    " (" +
                    GetLetterNameSafely(i) +
                    ") has no KochRoomVisibility component.",
                    this
                );
            }

            if(roomObjects[i].GetComponentInChildren<RoomKochLayoutSettings>() == null)
            {
                Debug.LogWarning(
                    bankName +
                    " room bank object at index " +
                    i +
                    " (" +
                    GetLetterNameSafely(i) +
                    ") has no RoomKochLayoutSettings component " +
                    "(searched self and children) - normal scale will fall back to " +
                    "whatever localScale the object happens to have.",
                    this
                );
            }
        }
    }

    //helper - return valid letter name for index or "unknown" if not in range
    private string GetLetterNameSafely(
        int letterIndex)
    {
        if(letterIndex < 0 ||
            letterIndex >=
            FractalNode.LetterNames.Length)
        {
            return "Unknown";
        }

        return FractalNode.LetterNames[
            letterIndex
        ];
    }

    //Helper to read normal scale. Uses GetComponentInChildren since RoomKochLayoutSettings may
    //live on a child "RoomVisual" object rather than on the room's top-level container itself
    private Vector3 GetRoomNormalLocalScale(GameObject room)
    {

        RoomKochLayoutSettings settings =
            room.GetComponentInChildren<RoomKochLayoutSettings>();

        if(settings != null)
        {
            return settings.normalLocalScale;
        }

        //Fallback to the game objs original scale
        return room.transform.localScale;
    }

    // Prepares incoming room for a CHILD transition by attaching it under the current node
    // via KochMotifNode.AttachChild
    private bool SetupRoomZoomIn(
        List<int> targetPath,
        VisualBank incomingBank,
        out GameObject incomingRoom,
        out Vector3 normalLocalScale,
        out Vector3 fittedLocalScale)
    {
        incomingRoom = null;
        normalLocalScale = Vector3.one;
        fittedLocalScale = Vector3.one;

        if(targetPath.Count == 0)
        {
            Debug.LogWarning(
                "SetupRoomZoomIn called with empty targetPath; " +
                "no room to animate.",
                this
            );

            return false;
        }

        int targetLetterIndex =
            targetPath[targetPath.Count - 1];

        incomingRoom =
            GetRoomObject(
                incomingBank,
                targetLetterIndex
            );

        if(incomingRoom == null)
        {
            Debug.LogError(
                "SetupRoomZoomIn: Incoming room is null for letter index " +
                targetLetterIndex +
                " in bank " +
                incomingBank,
                this
            );

            return false;
        }

        KochMotifNode incomingNode =
            incomingRoom.GetComponent<KochMotifNode>();

        if(incomingNode == null)
        {
            Debug.LogError(
                "SetupRoomZoomIn: incoming room " +
                incomingRoom.name +
                " has no KochMotifNode.",
                this
            );

            return false;
        }

        // Determine which node we are attaching 
        KochMotifNode parentNode =
            targetPath.Count == 1
                ? rootMotifNode
                : activeRoomNode;

        if(parentNode == null)
        {
            Debug.LogError(
                "SetupRoomZoomIn: no parent KochMotifNode available " +
                "(root node or active room node is missing).",
                this
            );

            return false;
        }

        // Root parents index children by letter (0..5). Room parents index children by slot
        // (0=previous,1=self,2=next) - resolve the letter to the correct slot automatically
        int slotOrLetterIndex =
            targetPath.Count == 1
                ? targetLetterIndex
                : GetChildSlotForLetter(parentNode, targetLetterIndex);

        if(slotOrLetterIndex < 0)
        {
            Debug.LogError(
                "SetupRoomZoomIn: letter " +
                GetLetterNameSafely(targetLetterIndex) +
                " is not a valid child slot of the current parent node.",
                this
            );

            return false;
        }

        // capture the normal (fully zoomed-in) scale BEFORE attaching/fitting
        normalLocalScale =
            GetRoomNormalLocalScale(
                incomingRoom
            );

        // AttachChild performs the real SetParent + sets localPosition/localRotation/localScale
        // to the CORRECT final values in one call
        parentNode.AttachChild(
            incomingNode,
            slotOrLetterIndex
        );

        fittedLocalScale =
            incomingRoom.transform.localScale;

        return true;
    }

    //Helper - given a room-mode parent node, find which child slot (0/1/2) a target letter
    //occupies. Returns -1 if not found
    private int GetChildSlotForLetter(
        KochMotifNode parentNode,
        int targetLetterIndex)
    {
        if(parentNode.IsRootNode)
        {
            return targetLetterIndex;
        }

        int[] childLetters =
            FractalNode.GetChildLetters(
                parentNode.RoomLetterIndex
            );

        for(int slot = 0; slot < childLetters.Length; slot++)
        {
            if(childLetters[slot] == targetLetterIndex)
            {
                return slot;
            }
        }

        return -1;
    }

    private IEnumerator ZoomIntoChildCoroutine(
       List<int> targetPath,
       VisualBank incomingBank)
    {
        // Root target has no child room to zoom into
        if(targetPath.Count == 0)
        {
            CompleteTransition(
                targetPath,
                incomingBank
            );

            yield break;
        }

        if(!SetupRoomZoomIn(
                targetPath,
                incomingBank,
                out GameObject incomingRoom,
                out Vector3 normalLocalScale,
                out Vector3 fittedLocalScale))
        {
            // Fallback: just complete instantly
            CompleteTransition(
                targetPath,
                incomingBank
            );

            yield break;
        }

        Transform roomTransform =
            incomingRoom.transform;

        // zoom in: start small on motif, grow to normal room scale
        // Position/rotation ALREADY correct
        // Only localScale is animated, relative to new parent
        // Stops drift/compounding bugs with old manual world-space approach
        Vector3 startScale =
            fittedLocalScale;

        Vector3 endScale =
            normalLocalScale;

        float duration =
            zoomInDuration;

        AnimationCurve curve =
            zoomInCurve;

        roomTransform.localScale =
            startScale;

        float elapsed = 0f;

        while(elapsed < duration)
        {
            float t =
                elapsed / duration;

            float eased =
                curve.Evaluate(t);

            roomTransform.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    endScale,
                    eased
                );

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        roomTransform.localScale =
            endScale;

        Debug.Log(
    "ZoomIntoChildCoroutine (before CompleteTransition): " +
    incomingRoom.name +
    " position=" + roomTransform.position +
    ", localPosition=" + roomTransform.localPosition +
    ", localScale=" + roomTransform.localScale,
    this
);

        CompleteTransition(
            targetPath,
            incomingBank
        );
    }

    private IEnumerator ZoomOutToParentCoroutine(
        List<int> targetPath,
        VisualBank incomingBank)
    {
        // For parent transitions, targetPath can be root (count == 0) or a higher-level room
        if(path.Count == 0)
        {
            // Already at root, nothing to zoom out
            CompleteTransition(
                targetPath,
                incomingBank
            );

            yield break;
        }

        // When going to root, we still need the current child room to zoom out
        // Use current letter index to get the room we are zooming from
        int currentLetterIndex =
            CurrentLetterIndex;

        if(currentLetterIndex < 0)
        {
            CompleteTransition(
                targetPath,
                incomingBank
            );

            yield break;
        }

        GameObject currentRoom =
            GetRoomObject(
                activeRoomBank,
                currentLetterIndex
            );

        if(currentRoom == null)
        {
            CompleteTransition(
                targetPath,
                incomingBank
            );

            yield break;
        }

        Transform roomTransform =
            currentRoom.transform;

        Vector3 normalLocalScale =
            GetRoomNormalLocalScale(
                currentRoom
            );

        // FIX: fittedLocalScale can NO LONGER be read from roomTransform.localScale here      
        Vector3 fittedLocalScale =
            normalLocalScale;

        if(activeRoomNode != null &&
            activeRoomNode.CurrentParentNode != null)
        {
            KochMotifNode parentNode =
                activeRoomNode.CurrentParentNode;

            int slotOrLetterIndex =
                GetChildSlotForLetter(
                    parentNode,
                    currentLetterIndex
                );

            if(slotOrLetterIndex >= 0 &&
                parentNode.TryGetChildFittedLocalScale(
                    activeRoomNode,
                    slotOrLetterIndex,
                    out float requiredScale))
            {
                fittedLocalScale =
                    Vector3.one * requiredScale;
            }
            else
            {
                Debug.LogWarning(
                    "ZoomOutToParentCoroutine: could not re-derive fitted scale for " +
                    currentRoom.name +
                    " - falling back to normalLocalScale (no shrink animation will be visible).",
                    this
                );
            }
        }

        // zoom out - start at normal, shrink into motif.
        Vector3 startScale =
            normalLocalScale;

        Vector3 endScale =
            fittedLocalScale;

        roomTransform.localScale =
            startScale;

        float duration =
            zoomOutDuration;

        AnimationCurve curve =
            zoomOutCurve;

        float elapsed = 0f;

        while(elapsed < duration)
        {
            float t =
                elapsed / duration;

            float eased =
                curve.Evaluate(t);

            roomTransform.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    endScale,
                    eased
                );

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        roomTransform.localScale =
            endScale;

        CompleteTransition(
            targetPath,
            incomingBank
        );
    }
}
