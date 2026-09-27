using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// Factory for a gamemode backed by a compiled plugin assembly.
/// </summary>
/// <remarks>
/// One factory per gamemode run, never shared: it owns the plugin's load context and
/// releases it at <see cref="End"/>, so a factory that outlived its session would
/// hold the assembly resident.
/// </remarks>
public class NativeGamemodeFactory(GamemodePlugin plugin, Dictionary<string, object>? config = null)
    : BaseGamemodeFactory
{
    public GamemodePlugin Plugin { get; private set; } = plugin;

    public override string GamemodeId => Plugin.Id;

    public override bool HasServerGamemode => Plugin.HasServerGamemode;

    public override IGamemode CreateGameMode(
        ClientGamemodeParameters parameters,
        IGamemodeContext gamemodeContext)
        => Plugin.CreateClient(parameters, gamemodeContext, config);

    public override IServerGamemode CreateServerGamemode(
        ServerGamemodeParameters parameters,
        IServerGamemodeContext context)
        => Plugin.CreateServer(parameters, context, config);

    /// <summary>
    /// Releases the plugin assembly. Safe to call more than once - both the client
    /// phase and the local race host tear their side down, and either may be first.
    /// </summary>
    public override void End()
    {
        Plugin.Unload();
        Plugin = null!;

#if DEBUG
        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        if (Plugin.IsAlive)
        {
            throw new InvalidOperationException(
                $"the plugin's assembly should be collectible once the race is over and the factory ended: {Plugin.Assembly}");
        }
#endif
    }
}