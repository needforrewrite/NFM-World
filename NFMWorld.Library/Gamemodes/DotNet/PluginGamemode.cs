using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorldLibrary.Gamemodes.DotNet;

public abstract class PluginGamemode(
    ClientGamemodeParameters clientGamemodeParameters,
    IGamemodeContext gamemodeContext,
    string gamemodeId,
    Dictionary<string, object>? config = null)
    : BaseClientGamemode(clientGamemodeParameters, gamemodeContext)
{
    public Dictionary<string, object>? Config { get; } = config;

    public override string GamemodeId { get; } = gamemodeId;
}