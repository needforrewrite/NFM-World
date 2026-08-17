using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class ClientGamemodeParameters
{
    public required IReadOnlyList<ClientSidePlayerInfo> Players { get; init; }
}