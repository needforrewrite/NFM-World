using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorldLibrary.Gamemodes.DotNet;

[PluginVisible(Visible.IncludingAllMembersOfType)]
public abstract class PluginGamemode(
    ClientGamemodeParameters clientGamemodeParameters,
    IGamemodeContext gamemodeContext,
    string gamemodeId,
    Dictionary<string, object>? config = null)
    : BaseClientGamemode(clientGamemodeParameters, gamemodeContext)
{
    [PluginVisible] public Dictionary<string, object>? Config { get; } = config;

    public override string GamemodeId { get; } = gamemodeId;
}