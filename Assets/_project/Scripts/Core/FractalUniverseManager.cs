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

        // Temporary immediate transition.
        // Replace this with a coroutine later.
        CompleteTransition(
            targetPath,
            incomingBank
        );
    }

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

        SetBankActive(
            incomingBank,
            false
        );

        incomingRoom.SetActive(true);
    }

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

    private VisualBank GetIncomingBank()
    {
        if(activeRoomBank == VisualBank.Odd)
            return VisualBank.Even;

        return VisualBank.Odd;
    }

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

    private bool IsValidRootChild(
        int targetLetterIndex)
    {
        return targetLetterIndex >= 0
            && targetLetterIndex <
            FractalNode.LetterNames.Length;
    }

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

    private bool IsValidLetterIndex(
        int letterIndex)
    {
        return letterIndex >= 0
            && letterIndex <
            FractalNode.LetterNames.Length;
    }

    private string GetCurrentLetterName()
    {
        if(CurrentLetterIndex < 0)
            return "Root";

        return FractalNode.LetterNames[
            CurrentLetterIndex
        ];
    }

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
    }

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
}