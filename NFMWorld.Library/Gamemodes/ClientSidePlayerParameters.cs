using Lua;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// The parameters used to construct a <see cref="ClientSidePlayer"/> in the client gamemode.
/// </summary>
[LuaVisible]
public partial class ClientSidePlayerParameters
{
    public required string PlayerName { get; init; } = "Player";
    public required string CarName { get; init; } = "nfmm/radicalone";
    public required Color3 Color { get; init; } = new Color3(255, 0, 0);
    /// <summary>
    /// If true, player is controlled by AI.
    /// </summary>
    public required bool IsBot { get; init; } = false;
    /// <summary>
    /// Whether the player is the client player, i.e. the player that hosts the client gamemode.
    /// </summary>
    public required bool IsClientPlayer { get; init; } = false;
}