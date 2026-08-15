using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Gamemodes.RaceHost;

/// <summary>
/// The transport-agnostic link between a race and its host.
/// <see cref="LocalRaceHost"/> runs the server gamemode in-process for
/// singleplayer; the game project provides a network host that bridges to the
/// remote Game Master. The race phase only ever talks to this interface, so
/// singleplayer and multiplayer share one code path.
/// </summary>
public interface IRaceHost : IDisposable
{
    /// <summary>True once the host link is ready (always true for local play).</summary>
    bool IsConnected { get; }

    /// <summary>Pump incoming host traffic and server ticks. Fires the events below.</summary>
    void Update();

    /// <summary>Send a gamemode-specific event to the host.</summary>
    void SendServerEvent(ReadOnlyMemory<byte> payload);

    /// <summary>Send the local player's car state to the host.</summary>
    void SendPlayerState(PlayerState state);

    /// <summary>Fired when the race may begin (all players loaded).</summary>
    event Action? RaceCanStart;

    /// <summary>Fired when the race failed to start.</summary>
    event Action? RaceFailedToStart;

    /// <summary>Fired when another player's car state arrives.</summary>
    event Action<int, PlayerState>? PlayerStateReceived;

    /// <summary>Fired when a gamemode-specific event arrives from the host.</summary>
    event Action<ReadOnlyMemory<byte>>? ServerEventReceived;

    /// <summary>Fired with authoritative results when the host ends the race.</summary>
    event Action<RaceResults>? GameFinished;
}
