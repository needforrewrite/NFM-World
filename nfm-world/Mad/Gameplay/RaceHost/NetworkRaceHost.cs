using NFMWorldLibrary;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using S2C_PlayerState = NFMWorldLibrary.Multiplayer.Packets.S2C.S2C_PlayerState;

namespace NFMWorld.Gameplay.RaceHost;

/// <summary>
/// Bridges the race phase to the remote Game Master over an
/// <see cref="IMultiplayerClientTransport"/>. Translates C2S/S2C packets
/// into <see cref="IRaceHost"/> events.
/// </summary>
public sealed class NetworkRaceHost : IRaceHost
{
    private readonly IMultiplayerClientTransport _transport;
    private readonly Guid _joinToken;
    private readonly Dictionary<Guid, int> _playerIndexById = [];
    private bool _sentRaceLoaded;

    public NetworkRaceHost(IMultiplayerClientTransport transport, MatchGameplayInfo session, Guid joinToken)
    {
        _transport = transport;
        _joinToken = joinToken;

        // The roster is fixed for the race, so resolve it once. Relay carries the player's id,
        // but the gamemode addresses players by array position - and that array is the roster
        // in slot order, so the two have to agree.
        var index = 0;
        foreach (var info in session.InSlotOrder())
            _playerIndexById[info.Id] = index++;

        // The Game Master dropped our connection index when the link went away, so it has to be
        // told which player this is again before it can relay anything. Clearing the flag lets
        // Update re-send it on the next tick, by which point the transport really is connected.
        transport.Reconnected += OnReconnected;
    }

    private void OnReconnected() => _sentRaceLoaded = false;

    public bool IsConnected => _transport.State == ClientState.Connected;

    /// <summary>True once the race connection dropped and reconnection gave up.</summary>
    public bool ReconnectFailed => _transport.ReconnectFailed;

    public event Action? RaceCanStart;
    public event Action? RaceFailedToStart;
    public event Action<int, PlayerState>? PlayerStateReceived;
    public event Action<ReadOnlyMemory<byte>>? ServerEventReceived;
    public event Action<RaceResults>? GameFinished;

    public void Update()
    {
        if (!_sentRaceLoaded && IsConnected)
        {
            _sentRaceLoaded = true;
            _transport.SendPacketToServer(new C2S_RaceLoaded { JoinToken = _joinToken });
        }

        foreach (var packet in _transport.GetNewPackets())
        {
            switch (packet)
            {
                case S2C_RaceCanStart:
                    RaceCanStart?.Invoke();
                    break;
                case S2C_RaceFailedToStart:
                    RaceFailedToStart?.Invoke();
                    break;
                case S2C_PlayerState playerState:
                    // An id not in this match (version skew, a late join, a replayed or
                    // malicious packet) must not throw in the game loop.
                    if (_playerIndexById.TryGetValue(playerState.PlayerId, out var carIndex))
                        PlayerStateReceived?.Invoke(carIndex, playerState.State);
                    break;
                case S2C_ServerEvent serverEvent:
                    try
                    {
                        ServerEventReceived?.Invoke(serverEvent.Payload);
                    }
                    finally
                    {
                        serverEvent.Dispose();
                    }
                    break;
                case S2C_GameFinished gameFinished:
                    GameFinished?.Invoke(gameFinished.Results);
                    break;
            }
        }
    }

    public void SendServerEvent(ReadOnlyMemory<byte> payload)
        => _transport.SendPacketToServer(new C2S_ClientEvent { Payload = payload.ToArray() }, reliable: true);

    public void SendPlayerState(PlayerState state, Guid? playerId = null)
        => _transport.SendPacketToServer(new C2S_PlayerState
        {
            State = state,
            // Null is the sender's own car; a bot the client simulates sends its own id.
            PlayerId = playerId ?? Guid.Empty
        }, false);

    public void Dispose()
    {
        _transport.Reconnected -= OnReconnected;
        _transport.Stop();
    }
}
