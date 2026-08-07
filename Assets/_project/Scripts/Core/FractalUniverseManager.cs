//handles traversal between room instances

using UnityEngine;

public class FractalUniverseManager : MonoBehaviour
{
    [Header("State")]
    public TraversalState CurrentState = TraversalState.Exploration;

    [Header("Current Node")]
    public FractalUniverseNode CurrentNode;

    public bool CanTraverse => CurrentState == TraversalState.Exploration;

    public void RequestTraverseToChild(int childIndex)
    {
        if(!CanTraverse)
            return;
        if(CurrentNode == null)
            return;

        FractalUniverseNode targetNode = CurrentNode.GetChild(childIndex);
        if(targetNode == null)
            return;

        StartTraversal(targetNode);
    }

    public void RequestTraverseToParent()
    {
        if(!CanTraverse)
            return;
        if(CurrentNode == null)
            return;
        if(CurrentNode.Parent == null)
            return;

        StartTraversal(CurrentNode.Parent);
    }

    private void StartTraversal(FractalUniverseNode targetNode)
    {
        CurrentState = TraversalState.Transitioning;

        // TODO: swap visuals, animate transition, load room content

        CurrentNode = targetNode;
        CurrentState = TraversalState.Exploration;
    }
}