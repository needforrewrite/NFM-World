namespace NFMWorldLibrary.Gamemodes.DotNet;

public static class GamemodeUtils
{
    public static int CountdownInterval => (int)(10 * (1 / NFMWorldLibrary.Physics.PHYSICS_MULTIPLIER));
}