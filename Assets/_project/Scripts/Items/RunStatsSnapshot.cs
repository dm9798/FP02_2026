// RunStatsSnapshot - static data holder carrying the final run stats from the gameplay
// scene into the Game Over scene
public static class RunStatsSnapshot
{
    public static int DungeonDepthReached
    {
        get; private set;
    }
    public static int RoomsVisited
    {
        get; private set;
    }
    public static int MonstersSlain
    {
        get; private set;
    }

    // Called once by PlayerHealth.Die immediately before loading the Game Over scene
    public static void Capture(int dungeonDepthReached, int roomsVisited, int monstersSlain)
    {
        DungeonDepthReached = dungeonDepthReached;
        RoomsVisited = roomsVisited;
        MonstersSlain = monstersSlain;
    }
}