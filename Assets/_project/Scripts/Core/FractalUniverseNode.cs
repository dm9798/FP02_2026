//In-game room instance

using System.Collections.Generic;
using UnityEngine;

public class FractalUniverseNode : MonoBehaviour
{
    [Header("Logical Identity")]
    [SerializeField] private int letterIndex;

    [Header("Tree Relationships")]
    public FractalUniverseNode Parent;
    public List<FractalUniverseNode> Children = new();

    public int LetterIndex => letterIndex;

    public string LetterName =>
        FractalNode.LetterNames[letterIndex];

    public int ChildCount =>
        Children != null ? Children.Count : 0;

    public FractalUniverseNode GetChild(int index)
    {
        if(Children == null)
            return null;
        if(index < 0 || index >= Children.Count)
            return null;

        return Children[index];
    }

    public void BuildChildrenFromLetter()
    {
        Children.Clear();

        int[] childLetters =
            FractalNode.GetChildLetters(letterIndex);

        foreach(int childLetterIndex in childLetters)
        {
            //to be implemented
            // lookup or factory to find/create corresponding FractalUniverseNode.

        }
    }
}