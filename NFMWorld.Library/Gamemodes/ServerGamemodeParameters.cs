using NFMWorld.PluginSystem;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Backend.Gamemodes;

[PluginVisible]
public class ServerGamemodeParameters
{
    [PluginVisible] public required IReadOnlyList<ServerSidePlayerInfo> Players { get; init; }
}