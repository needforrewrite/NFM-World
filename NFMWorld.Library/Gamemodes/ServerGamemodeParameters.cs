using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class ServerGamemodeParameters
{
    public required IReadOnlyList<ServerSidePlayerInfo> Players { get; init; }
}