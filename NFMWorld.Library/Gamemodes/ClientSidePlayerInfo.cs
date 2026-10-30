using System.Diagnostics.CodeAnalysis;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// The information for <see cref="ClientSidePlayer"/> in the client gamemode.
/// </summary>
[PluginVisible]
public partial class ClientSidePlayerInfo
{
    [PluginVisible] public required string PlayerName { get; init; } = "Player";
    [PluginVisible] public required string CarName { get; init; } = "nfmm/radicalone";
    [PluginVisible] public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    /// <summary>
    /// If true, player is controlled by AI.
    /// </summary>
    [PluginVisible] public required bool IsBot { get; init; } = false;
    /// <summary>
    /// Whether the player is the client player, i.e. the player that hosts the client gamemode.
    /// </summary>
    [PluginVisible] public required bool IsClientPlayer { get; init; } = false;

    public ClientSidePlayerInfo()
    {
    }

    [SetsRequiredMembers]
    [PluginVisible]
    public ClientSidePlayerInfo(string playerName, string carName, Color3 color, bool isBot, bool isClientPlayer)
    {
        PlayerName = playerName;
        CarName = carName;
        Color = color;
        IsBot = isBot;
        IsClientPlayer = isClientPlayer;
    }
}