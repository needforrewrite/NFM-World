using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// Factory for a gamemode backed by a compiled plugin assembly.
/// </summary>
public class NativeGamemodeFactory(GamemodePlugin plugin, Dictionary<string, object>? config = null)
    : BaseGamemodeFactory
{
    public GamemodePlugin Plugin => plugin;

    public override string GamemodeId => plugin.Id;

    public override bool HasServerGamemode => plugin.HasServerGamemode;

    public override IGamemode CreateGameMode(
        ClientGamemodeParameters parameters,
        IGamemodeContext gamemodeContext)
        => plugin.CreateClient(parameters, gamemodeContext, config);

    public override IServerGamemode CreateServerGamemode(
        ServerGamemodeParameters parameters,
        IServerGamemodeContext context)
        => plugin.CreateServer(parameters, context, config);
}
