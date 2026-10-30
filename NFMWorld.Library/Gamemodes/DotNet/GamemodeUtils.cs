using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes.DotNet;

[PluginVisible]
public static class GamemodeUtils
{
    [PluginVisible]
    public static int CountdownInterval => (int)(10 * (1 / NFMWorldLibrary.Physics.PHYSICS_MULTIPLIER));
}