namespace NFMWorldLibrary.Backend.Gamemodes;

public class BaseGamemodeParameters
{
    public required int PlayerCarIndex { get; init; }
    public required IReadOnlyList<PlayerParameters> Players { get; init; }
}

public class PlayerParameters
{
    public required string PlayerName { get; init; } = "Player";
    public required string CarName { get; init; } = "nfmm/radicalone";
    public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    public required bool IsBot { get; init; } = false;
    // team, isbot, etc
}