public class FractalNode
{
    public int letter;
    //public static readonly string[] LetterNames = { "A", "B", "C", "D", "E", "F" };
    public static readonly string[] LetterNames = { "A", "F", "E", "D", "C", "B" };

    public int[] GetChildLetters()
    {
        int prev = (letter - 1 + 6) % 6;
        int next = (letter + 1) % 6;
        return new int[] { prev, letter, next };
    }
}
