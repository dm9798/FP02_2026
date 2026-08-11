using System.Collections.Generic;
using UnityEngine;

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

    [Header("Root Motif Layout")]
    [SerializeField] private RootMotifLayout rootMotifLayout;

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

    //temp vars to stop multiple edge collisions between room transitions
    //to be removed following zoom implementation
    [Header("Traversal Protection")]
    [SerializeField] private float traversalCooldown = 0.2f;

    private float traversalBlockedUntil;

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
        currentState =
            TraversalState.Transitioning;

        VisualBank incomingBank =
            GetIncomingBank();

        PrepareIncomingVisual(
            targetPath,
            incomingBank
        );

        // Temporary immediate transition - to be replcaed        
        CompleteTransition(
            targetPath,
            incomingBank
        );
    }


    //Set up incoming visual (root/room),
    //align child room to target motif, activate correct room game obj
    private void PrepareIncomingVisual(
        List<int> targetPath,
        VisualBank incomingBank)
    {
        if(targetPath.Count == 0)
        {
            SetBankActive(
                incomingBank,
                false
            );

            if(rootVisual != null)
            {
                rootVisual.SetActive(true);
            }

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

        bool isChildTransition =
            targetPath.Count ==
            path.Count + 1;

        if(isChildTransition)
        {
            bool targetFound =
                TryGetTargetMotif(
                    targetPath,
                    targetLetterIndex,
                    out Vector3 targetPosition,
                    out float targetRadius
                );

            if(targetFound)
            {
                AlignIncomingRoom(
                    incomingRoom,
                    targetPosition,
                    targetRadius
                );
            }
        }

        SetBankActive(
            incomingBank,
            false
        );

        incomingRoom.SetActive(true);
    }

    //Finalize traversal - deactivate old room, activate new visual
    //update path & active bank
    private void CompleteTransition(
        List<int> targetPath,
        VisualBank incomingBank)
    {
        if(activeRoomBank != VisualBank.None)
        {
            SetBankActive(
                activeRoomBank,
                false
            );
        }

        if(targetPath.Count == 0)
        {
            SetBankActive(
                incomingBank,
                false
            );

            if(rootVisual != null)
            {
                rootVisual.SetActive(true);
            }

            activeRoomBank =
                VisualBank.None;
        }
        else
        {
            if(rootVisual != null)
            {
                rootVisual.SetActive(false);
            }

            int targetLetterIndex =
                targetPath[targetPath.Count - 1];

            GameObject targetRoom =
                GetRoomObject(
                    incomingBank,
                    targetLetterIndex
                );

            if(targetRoom != null)
            {
                targetRoom.SetActive(true);
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

    //helper - activate/deactivate all room game objs in selected bank
    private void SetBankActive(
        VisualBank bank,
        bool isActive)
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
            if(roomObject != null)
            {
                roomObject.SetActive(isActive);
            }
        }
    }

    //Helper - clear traversal path, disables all rooms, show root visual
    private void ResetToRoot()
    {
        path.Clear();

        SetBankActive(
            VisualBank.Odd,
            false
        );

        SetBankActive(
            VisualBank.Even,
            false
        );

        if(rootVisual != null)
        {
            rootVisual.SetActive(true);
        }

        activeRoomBank =
            VisualBank.None;

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

        if(rootMotifLayout == null)
        {
            Debug.LogWarning(
                "RootMotifLayout has not been assigned.",
                this
            );
        }        
    }

    //helper - validate selected roomank array of errors
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
            }
        }
    }

    //helper - return valid letter name for index or "unknown" if not in range.
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

    //helper - solve world-space pos and radius target motif for given path & letter
    //checks from the root or current room
    private bool TryGetTargetMotif(
    List<int> targetPath,
    int targetLetterIndex,
    out Vector3 targetPosition,
    out float targetRadius)
    {
        targetPosition =
            Vector3.zero;

        targetRadius =
            0f;

        if(targetPath.Count == 1)
        {
            if(rootMotifLayout == null)
            {
                Debug.LogWarning(
                    "RootMotifLayout is missing.",
                    this
                );

                return false;
            }

            RootMotifData rootMotif =
                rootMotifLayout.GetMotif(
                    targetLetterIndex
                );

            if(rootMotif == null)
            {
                return false;
            }

            targetPosition =
                rootMotifLayout.GetMotifWorldPosition(
                    targetLetterIndex
                );

            targetRadius =
                rootMotifLayout.GetMotifWorldRadius(
                    targetLetterIndex
                );

            return true;
        }

        if(activeRoomBank == VisualBank.None)
        {
            return false;
        }

        if(CurrentLetterIndex < 0)
        {
            return false;
        }

        GameObject currentRoom =
            GetRoomObject(
                activeRoomBank,
                CurrentLetterIndex
            );

        if(currentRoom == null)
        {
            return false;
        }

        RoomMotifLayout currentLayout =
            currentRoom.GetComponent<RoomMotifLayout>();

        if(currentLayout == null)
        {
            Debug.LogWarning(
                "Current room has no RoomMotifLayout.",
                currentRoom
            );

            return false;
        }

        RoomMotifData roomMotif =
            currentLayout.GetMotifForLetter(
                targetLetterIndex
            );

        if(roomMotif == null)
        {
            Debug.LogWarning(
                "Target letter was not found in the " +
                "current room motif layout.",
                currentRoom
            );

            return false;
        }

        targetPosition =
            currentLayout.GetMotifWorldPositionForLetter(
                targetLetterIndex
            );

        targetRadius =
            GetRoomMotifWorldRadius(
                currentLayout,
                roomMotif
            );

        return true;
    }


    //Helper - convert motif’s local target radius into world space using layout’s set scale
    private float GetRoomMotifWorldRadius(
    RoomMotifLayout layout,
    RoomMotifData motif)
    {
        float averageScale =
            (
                Mathf.Abs(layout.transform.lossyScale.x)
                +
                Mathf.Abs(layout.transform.lossyScale.y)
            ) * 0.5f;

        return motif.targetRadius *
            averageScale;
    }

    //Helper - scales and pos incoming room so layout matches target motif’s world radius/pos
    private void AlignIncomingRoom(
      GameObject incomingRoom,
      Vector3 targetPosition,
      float targetRadius)
    {
        if(incomingRoom == null)
        {
            return;
        }

        RoomMotifLayout incomingLayout =
            incomingRoom.GetComponent<RoomMotifLayout>();

        if(incomingLayout == null)
        {
            Debug.LogWarning(
                "Incoming room has no RoomMotifLayout.",
                incomingRoom
            );

            return;
        }

        float incomingRadius =
            incomingLayout.GetLocalRoomRadius();

        if(incomingRadius <= 0f)
        {
            Debug.LogWarning(
                "Incoming room radius is invalid.",
                incomingRoom
            );

            return;
        }

        float parentScale =
            GetAverageParentScale(
                incomingRoom.transform
            );

        if(parentScale <= 0f)
        {
            parentScale = 1f;
        }

        float requiredLocalScale =
            targetRadius /
            (incomingRadius * parentScale);

        incomingRoom.transform.localScale =
            Vector3.one * requiredLocalScale;

        Vector3 correctedTargetPosition =
            RotateTargetPosition(
                targetPosition
            );

        incomingRoom.transform.position =
            correctedTargetPosition;

        Vector3 roomCenterWorld =
            incomingLayout.GetRoomWorldCenter();

        Vector3 centerOffset =
            roomCenterWorld -
            incomingRoom.transform.position;

        incomingRoom.transform.position -=
            centerOffset;
    }

    //Helper - calculate avg XY scale factor for incoming room’s parent transform
    private float GetAverageParentScale(
    Transform roomTransform)
    {
        if(roomTransform.parent == null)
            return 1f;

        Vector3 parentScale =
            roomTransform.parent.lossyScale;

        return (
            Mathf.Abs(parentScale.x)
            +
            Mathf.Abs(parentScale.y)
        ) * 0.5f;
    }

    //Helper - apply specific XY rotation/flip required correct room target position offset bug
    private Vector3 RotateTargetPosition(
    Vector3 originalPosition)
    {
        return new Vector3(
            -originalPosition.y,
            originalPosition.x,
            originalPosition.z
        );
    }

    //to be revisited - per-room config might be better solution
    private Vector3 GetRoomNormalLocalScale(GameObject room)
    {
      
        return room.transform.localScale;
    }
}