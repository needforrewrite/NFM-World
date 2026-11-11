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

    /// <summary>Sends a chat message from a client to all lobby clients.</summary>
    public void SendChatMessage(Guid senderId, string message)
    {
        var sender = players.Get(senderId);
        if (sender is not { IsAuthenticated: true, AccountId: > 0 }) return;

        Broadcast(
            new S2C_LobbyChatMessage
            {
                SenderId = senderId,
                Sender = sender.Name,
                Message = message
            }
        );
    }

    /// <summary>Broadcasts a system message (e.g., join/leave announcements).</summary>
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
}
