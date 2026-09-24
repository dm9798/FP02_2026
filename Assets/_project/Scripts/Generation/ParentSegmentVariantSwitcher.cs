using UnityEngine;


/// Lives on Room_Visual_X, as a sibling game object of game object containing RoomBoundaryGenerator/RoomDirector
/// This room prefab X can be ENTERED from exactly 3 possible parent letters -
/// itself (self slot), and its two ring-neighbours (prev/next slots)
/// So X needs exactly 3 deep variants, one per POSSIBLE EXIT LETTER a player could have just left from
/// to arrive here, plus 1 shallow variant for arriving from the root (root mode has no
/// prev/self/next slot concept at all - KochMotifAligner.TryGetRootChildLocalTransform places all 6 children directly by letterIndex
///
/// Lookup is keyed directly on the EXIT ROOM'S LETTER, not a numeric slot - per field-naming convention 

/// FUM calls RefreshParentSegmentVariant() once, right when THIS room becomes the newly-active
/// room - childNode's switcher in FractalUniverseManager.ZoomIntoChildCoroutine, and on newActive's switcher in ZoomOutToParentCoroutine. 
/// Pass the LETTER INDEX of whichever room/root the player just came from


/// Note for inspector:
/// the "prev/next" naming in ParentSegmentVariantSwitcher's exit-letter mapping is inverted relative to RoomBoundaryGenerator's 
/// own prevEdgeCollider/nextEdgeCollider  slot convention (which follows GetChildLetters directly)
/// Mapping that works:
// A: 1, 0, 5
// F: 2, 1, 0
// E: 3, 2, 1
// D: 4, 3, 2
// C: 5, 4, 3
// B: 0, 5, 4 



public class ParentSegmentVariantSwitcher : MonoBehaviour
{
    [Header("Shallow Variant (entered from the root)")]
    [SerializeField] private GameObject parentSegShallow;

    [Header("Deep Variants (entered from another room - one per possible exit letter)")]
    [SerializeField] private GameObject parentSegDeepPrevExit;   
    [SerializeField] private GameObject parentSegDeepSelfExit;
    [SerializeField] private GameObject parentSegDeepNextExit;

    [Header("Exit Letter Mapping (which letter index maps to which field above)")]
    [SerializeField] private int prevExitLetterIndex = -1;
    [SerializeField] private int selfExitLetterIndex = -1;
    [SerializeField] private int nextExitLetterIndex = -1;

    [Header("Wiring")]
    [SerializeField] private FractalUniverseManager universeManager;

    private void Awake()
    {
        if(parentSegShallow == null || parentSegDeepPrevExit == null ||
            parentSegDeepSelfExit == null || parentSegDeepNextExit == null)
        {
            Debug.LogError($"{name} ParentSegmentVariantSwitcher is missing one or more of " +
                            "its four variant references.", this);
        }

        if(prevExitLetterIndex < 0 || selfExitLetterIndex < 0 || nextExitLetterIndex < 0)
        {
            Debug.LogError($"{name} ParentSegmentVariantSwitcher has an unassigned exit " +
                            "letter mapping (prevExitLetterIndex/selfExitLetterIndex/" +
                            "nextExitLetterIndex) - deep variant lookup will fail.", this);
        }
    }

   
    //Mirrors RoomBoundaryGenerator's own Initialize(FractalUniverseManager) pattern    
    public void Initialize(FractalUniverseManager manager)
    {
        universeManager = manager;
    }

    // Enables exactly one of the four variants and disables the other three.
    // bool cameFromRoot - True if the player just arrived here directly from the dungeon root (shallow)
    // int exitLetterIndex - the letter index (per FractalNode.LetterNames) of the room the player just left to arrive here
    // Ignored when cameFromRoot is true
   
    public void RefreshParentSegmentVariant(bool cameFromRoot, int exitLetterIndex = -1)
    {
        if(parentSegShallow == null || parentSegDeepPrevExit == null ||
            parentSegDeepSelfExit == null || parentSegDeepNextExit == null)
        {
            Debug.LogError($"{name} ParentSegmentVariantSwitcher cannot refresh - missing " +
                            "one or more variant references.", this);
            return;
        }

        if(cameFromRoot)
        {
            parentSegShallow.SetActive(true);
            parentSegDeepPrevExit.SetActive(false);
            parentSegDeepSelfExit.SetActive(false);
            parentSegDeepNextExit.SetActive(false);
            return;
        }

        parentSegShallow.SetActive(false);

        if(exitLetterIndex == prevExitLetterIndex)
        {
            parentSegDeepPrevExit.SetActive(true);
            parentSegDeepSelfExit.SetActive(false);
            parentSegDeepNextExit.SetActive(false);
        }
        else if(exitLetterIndex == selfExitLetterIndex)
        {
            parentSegDeepPrevExit.SetActive(false);
            parentSegDeepSelfExit.SetActive(true);
            parentSegDeepNextExit.SetActive(false);
        }
        else if(exitLetterIndex == nextExitLetterIndex)
        {
            parentSegDeepPrevExit.SetActive(false);
            parentSegDeepSelfExit.SetActive(false);
            parentSegDeepNextExit.SetActive(true);
        }
        else
        {
            Debug.LogError($"{name} ParentSegmentVariantSwitcher.RefreshParentSegmentVariant " +
                            $"received exitLetterIndex={exitLetterIndex}, which doesn't match " +
                            $"any of this room's configured exit mappings " +
                            $"(prev={prevExitLetterIndex}, self={selfExitLetterIndex}, " +
                            $"next={nextExitLetterIndex}). Hiding all deep variants instead " +
                            "of guessing.", this);
            parentSegDeepPrevExit.SetActive(false);
            parentSegDeepSelfExit.SetActive(false);
            parentSegDeepNextExit.SetActive(false);
        }
    }
}