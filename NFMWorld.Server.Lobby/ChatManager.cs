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
    /// Broadcasts a chat line tagged with its channel and returns it for any race relay.
    /// </summary>
    public S2C_LobbyChatMessage? SendChatMessage(Guid senderId, string message, uint sessionId)
    {
        var sender = players.Get(senderId);

        if (sender is not { IsAuthenticated: true, AccountId: > 0 })

            return null;

        var isSpectator = sessionId != 0 && sender.InSession?.SessionIndex != sessionId;
        var line = new S2C_LobbyChatMessage
        {
            SenderId = senderId,
            Sender = isSpectator ? $"(SPEC) {sender.Name}" : sender.Name,
            Message = message,
            SessionId = sessionId,
            Color = sender.Color
        };
        Broadcast(line);

        return line;
    }

    /// <summary>
    /// Relays a line tagged with its match, which the client uses to select its transcript.
    /// </summary>
    public void SendToSession(uint sessionId, S2C_LobbyChatMessage message, bool openToLobby = false)
    {
        message.SessionId = sessionId;

        if (openToLobby)
        {
            Broadcast(message);

            return;
        }

        transport.SendPacketToClients(
            players.All
                .Where(p => p.Value.IsAuthenticated
                            && p.Value.AccountId > 0
                            && p.Value.InSession?.SessionIndex
                            == sessionId
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

        if (player is null)

            return;
        transport.SendPacketToClient(player.ClientIndex, new S2C_ModerationNotice { Message = message, Fatal = fatal });
    }

    /// <summary>The actor's own command outcome.</summary>
    public void SendResult(Guid playerId, bool ok, string message)
    {
        var player = players.Get(playerId);

        if (player is null)

            return;
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
