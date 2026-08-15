using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class GamemodeParameters
{
    public required IReadOnlyList<ClientSidePlayerParameters> Players { get; init; }
}