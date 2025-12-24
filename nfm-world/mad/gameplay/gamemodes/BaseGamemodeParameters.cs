namespace NFMWorld.Mad.gamemodes;

public class BaseGamemodeParameters
{
    public int PlayerCarIndex { get; init; }
    public IReadOnlyList<PlayerParameters> Players { get; init; }
}

public class PlayerParameters
{
    public required string PlayerName { get; init; } = "Player";
    public required string CarName { get; init; } = "nfmm/radicalone";
    public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    // team, isbot, etc
}