using System.Diagnostics.CodeAnalysis;
using Lua;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// The parameters used to construct a <see cref="ClientSidePlayer"/> in the client gamemode.
/// </summary>
[LuaVisible]
public partial class ClientSidePlayerParameters
{
    [LuaName] public required string PlayerName { get; init; } = "Player";
    [LuaName] public required string CarName { get; init; } = "nfmm/radicalone";
    [LuaName] public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    /// <summary>
    /// If true, player is controlled by AI.
    /// </summary>
    [LuaName] public required bool IsBot { get; init; } = false;
    /// <summary>
    /// Whether the player is the client player, i.e. the player that hosts the client gamemode.
    /// </summary>
    [LuaName] public required bool IsClientPlayer { get; init; } = false;

    public ClientSidePlayerParameters()
    {
    }

    [SetsRequiredMembers]
    [LuaName("new")]
    public ClientSidePlayerParameters(string playerName, string carName, Color3 color, bool isBot, bool isClientPlayer)
    {
        PlayerName = playerName;
        CarName = carName;
        Color = color;
        IsBot = isBot;
        IsClientPlayer = isClientPlayer;
    }
}