using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class GamemodeParameters
{
    public required IReadOnlyList<PlayerParameters> Players { get; init; }
}