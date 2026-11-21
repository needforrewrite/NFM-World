using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Handles chat messages and system announcements.
/// </summary>
public class ChatManager(IMultiplayerServerTransport transport, PlayerRegistry players)
{
    private void Broadcast(S2C_LobbyChatMessage packet) => transport.SendPacketToClients(
        players.All
            .Where(p => p.Value.IsAuthenticated && p.Value.AccountId > 0)
            .Select(p => p.Key)
            .ToArray(),
        packet
    );

    /// <summary>
    /// Sends a chat message from a client. Room chat (session zero) reaches the whole lobby;
    /// match chat only reaches the members of the game it was sent in.
    /// </summary>
    public void SendChatMessage(Guid senderId, string message, uint sessionId)
    {
        var sender = players.Get(senderId);
        if (sender is not { IsAuthenticated: true, AccountId: > 0 }) return;

        if (sessionId == 0)
        {
            Broadcast(
                new S2C_LobbyChatMessage
                {
                    SenderId = senderId,
                    Sender = sender.Name,
                    Message = message,
                    Color = sender.Color
                }
            );
            return;
        }

        // A client can name any session id; only its own game may answer for it.
        if (sender.InSession?.SessionIndex != sessionId)
            return;

        SendToSession(
            sessionId,
            new S2C_LobbyChatMessage
            {
                SenderId = senderId,
                Sender = sender.Name,
                Message = message,
                SessionId = sessionId,
                Color = sender.Color
            }
        );
    }

    /// <summary>
    /// Relays one match-lobby line to that game's members. Used for both member chat and the
    /// lines a game master forwards out of a running race.
    /// </summary>
    public void SendToSession(uint sessionId, S2C_LobbyChatMessage message)
    {
        message.SessionId = sessionId;
        transport.SendPacketToClients(
            players.All
                .Where(p => p.Value.IsAuthenticated
                            && p.Value.AccountId > 0
                            && p.Value.InSession?.SessionIndex == sessionId
                )
                .Select(p => p.Key)
                .ToArray(),
            message
        );
    }

    /// <summary>Broadcasts a system message (e.g., join/leave announcements) to the room.</summary>
    public void BroadcastSystem(string message)
    {
        Broadcast(
            new S2C_LobbyChatMessage
            {
                SenderId = Guid.Empty,
                Message = message,
                Sender = "<System>"
            }
        );
    }

    /// <summary>A system line in one game's match transcript.</summary>
    public void SendSystemToSession(uint sessionId, string message)
        => SendToSession(
            sessionId,
            new S2C_LobbyChatMessage
            {
                SenderId = Guid.Empty,
                Sender = "<System>",
                Message = message
            }
        );

    /// <summary>A moderation notice to a single client.</summary>
    public void SendNotice(Guid playerId, string message, bool fatal = false)
    {
        var player = players.Get(playerId);
        if (player is null) return;
        transport.SendPacketToClient(player.ClientIndex, new S2C_ModerationNotice { Message = message, Fatal = fatal });
    }

    /// <summary>The actor's own command outcome.</summary>
    public void SendResult(Guid playerId, bool ok, string message)
    {
        var player = players.Get(playerId);
        if (player is null) return;
        transport.SendPacketToClient(player.ClientIndex, new S2C_ModerationResult { Ok = ok, Message = message });
    }

    /// <summary>Broadcasts a purge for one sender's lines to every authenticated client.</summary>
    public void PurgeLines(Guid playerId)
    {
        transport.SendPacketToClients(
            players.All
                .Where(p => p.Value.IsAuthenticated && p.Value.AccountId > 0)
                .Select(p => p.Key)
                .ToArray(),
            new S2C_ChatPurge { SenderId = playerId }
        );
    }

    /// <summary>A lobby system line visible only to staff.</summary>
    public void BroadcastToStaff(string message)
    {
        transport.SendPacketToClients(
            players.All
                .Where(p => p.Value.IsAuthenticated && p.Value.AccountId > 0 && p.Value.Role >= UserRole.Moderator)
                .Select(p => p.Key)
                .ToArray(),
            new S2C_LobbyChatMessage
            {
                SenderId = Guid.Empty,
                Sender = "<Staff>",
                Message = message
            }
        );
    }
}
