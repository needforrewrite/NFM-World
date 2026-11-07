using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes.DotNet;

[PluginVisible]
public static class GamemodeUtils
{
    /// <summary>
    /// Ticks in one countdown second on the client, which runs at <see cref="Physics.TargetTps"/>.
    /// </summary>
    [PluginVisible]
    public static int CountdownInterval => (int)(10 * (1 / NFMWorldLibrary.Physics.PHYSICS_MULTIPLIER));

    /// <summary>The Game Master's server-gamemode tick rate.</summary>
    [PluginVisible]
    public const int ServerTps = 20;

    /// <summary>
    /// Ticks in one countdown step on a server gamemode. <see cref="IServerGamemode.GameTick"/>
    /// runs at the Game Master's 20 TPS, not the client's, so the client's interval made each
    /// countdown step take three times as long.
    /// </summary>
    [PluginVisible]
    public static int ServerCountdownInterval =>
        (int)Math.Round(10f / NFMWorldLibrary.Physics.OriginalTps * ServerTps);
}