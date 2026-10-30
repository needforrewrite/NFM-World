using NFMWorld.PluginSystem;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.AI;

/// <summary>
/// Base class for an AI loaded from a compiled plugin assembly
/// (<c>data/ais/&lt;id&gt;/ai.dll</c>).
/// </summary>
/// <remarks>
/// Mirrors <c>PluginGamemode</c>: the assembly is discovered by folder and loaded
/// into the same collectible context, so a bot is only resident while its race is.
/// Instance state belongs on the instance - two bots on one track must not share a
/// target node.
/// </remarks>
[PluginVisible]
[method: PluginVisible]
public abstract class PluginAi(ClientSidePlayer player, string aiId, BaseClientGamemode gamemode) : BaseAi
{
    [PluginVisible] public string AiId { get; } = aiId;

    /// <summary>The player this bot drives.</summary>
    [PluginVisible] public ClientSidePlayer Player { get; } = player;

    /// <summary>The car this bot drives, or null before the gamemode creates it.</summary>
    [PluginVisible] public BackendCar? Car => Player.Car;

    [PluginVisible] public IReadOnlyList<ClientSidePlayer> Players => gamemode.Players;

    [PluginVisible] public BackendStage CurrentStage => gamemode.CurrentStage;
}
