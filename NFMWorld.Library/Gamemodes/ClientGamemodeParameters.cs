using NFMWorld.PluginSystem;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

[PluginVisible]
public class ClientGamemodeParameters
{
    [PluginVisible] public required IReadOnlyList<ClientSidePlayerInfo> Players { get; init; }
}