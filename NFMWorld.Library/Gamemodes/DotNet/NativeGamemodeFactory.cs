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
    // Read up front, because End() drops the plugin and every one of these is a read of
    // it, and the plugin holds the load context that keeps the assembly resident.
    private readonly string _gamemodeId = plugin.Id;
    private readonly bool _hasServerGamemode = plugin.HasServerGamemode;

    private GamemodePlugin? _plugin = plugin;

    /// <summary>
    /// The plugin driving this run. Throws once <see cref="End"/> has released it, since
    /// holding on to it is exactly what would keep the assembly resident.
    /// </summary>
    public GamemodePlugin Plugin => _plugin
        ?? throw new InvalidOperationException($"Gamemode plugin '{_gamemodeId}' has already been released.");

    public override string GamemodeId => _gamemodeId;

    public override bool HasServerGamemode => _hasServerGamemode;

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
    /// <remarks>
    /// Releases, and nothing else. Verifying here that the assembly really did collect is
    /// tempting, since the failure is otherwise silent - but a collection forced from
    /// inside this call always finds the assembly alive: the frames up the stack to here
    /// are still live roots, so the check would be reporting the caller's own reference.
    /// Whether the assembly was released is only answerable after the teardown has
    /// returned, which is where <c>GamemodePluginUnloadTests</c> checks it.
    /// </remarks>
    public override void End()
    {
        if (_plugin is null)
            return;

        _plugin.Unload();
        _plugin = null;
    }
}
