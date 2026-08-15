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
public sealed class NetworkRaceHost(
    IMultiplayerClientTransport transport,
    MatchGameplayInfo session,
    Guid joinToken) : IRaceHost
{
    private bool _sentRaceLoaded;

    public bool IsConnected => transport.State == ClientState.Connected;

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
            transport.SendPacketToServer(new C2S_RaceLoaded { JoinToken = joinToken });
        }

        foreach (var packet in transport.GetNewPackets())
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
                {
                    var carIndex = session.Players
                        .First(e => e.Value.Id == playerState.PlayerId)
                        .Key;
                    PlayerStateReceived?.Invoke(carIndex, playerState.State);
                    break;
                }
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
        => transport.SendPacketToServer(new C2S_ClientEvent { Payload = payload.ToArray() }, reliable: true);

    public void SendPlayerState(PlayerState state)
        => transport.SendPacketToServer(new C2S_PlayerState { State = state }, false);

    public void Dispose()
        => transport.Stop();
}
