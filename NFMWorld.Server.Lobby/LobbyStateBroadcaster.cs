using System.Collections.Concurrent;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Builds and broadcasts <see cref="S2C_LobbyState"/> snapshots to all connected clients.
/// </summary>
public class LobbyStateBroadcaster(
    IMultiplayerServerTransport transport,
    PlayerRegistry players,
    SessionManager sessions
)
{
    /// <summary>Broadcasts a full lobby state snapshot to every connected client.</summary>
    public void BroadcastToAll()
    {
        foreach (var (clientIndex, player) in players.All)
        {
            if (!player.IsAuthenticated || player.AccountId <= 0)
                continue;

            var packet = BuildSnapshot(player.Id);
            transport.SendPacketToClient(clientIndex, packet);
        }
    }

    /// <summary>Builds a <see cref="S2C_LobbyState"/> for a specific client.</summary>
    public S2C_LobbyState BuildSnapshot(Guid playerId)
    {
        var playerList = new List<ServerSidePlayerInfo>();
        var sessionList = new List<S2C_LobbyState.GameSession>();

        foreach (var (id, client) in players.All)
        {
            // A connection that has not authenticated yet is not a player: listing it showed
            // the registry's placeholder name until the handshake finished.
            if (!client.IsAuthenticated || client.AccountId <= 0)
                continue;

            playerList.Add(
                new ServerSidePlayerInfo
                {
                    Id = client.Id,
                    PlayerName = client.Name,
                    CarName = client.Vehicle,
                    Color = client.Color
                }
            );
        }

        foreach (var (_, session) in sessions.All)
        {
            // Copy the live membership dictionary: the packet is serialized off-thread
            // while joins and leaves mutate it.
            var players = new ConcurrentDictionary<byte, Guid>(session.Players);

            sessionList.Add(
                new S2C_LobbyState.GameSession
                {
                    Id = session.Id,
                    CreatorId = session.CreatorId,
                    CreatorName = session.CreatorName,
                    StageName = session.StageName,
                    MaxPlayers = session.MaxPlayers,
                    Players = players,
                    State = session.State
                }
            );
        }

        return new S2C_LobbyState
        {
            ClientId = playerId,
            Players = playerList,
            ActiveSessions = sessionList
        };
    }
}
