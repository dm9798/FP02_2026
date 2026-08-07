//Rule system for parent-child relationships

public class FractalNode
{
    public int letter;
    public static readonly string[] LetterNames = { "A", "F", "E", "D", "C", "B" };

   //fallback with no arg
    public int[] GetChildLetters()
    {
        return GetChildLetters(letter);
    }


    public static int[] GetChildLetters(int letterIndex)
    {
        int prev = (letterIndex - 1 + 6) % 6;
        int next = (letterIndex + 1) % 6;
        return new int[] { prev, letterIndex, next };
    }
}