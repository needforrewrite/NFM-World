using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Handles chat messages and system announcements.
/// </summary>
public class ChatManager(IMultiplayerServerTransport transport, PlayerRegistry players)
{
    /// <summary>Sends a chat message from a client to all lobby clients.</summary>
    public void SendChatMessage(Guid senderId, string message)
    {
        var sender = players.Get(senderId);
        if (sender is null) return;

        transport.BroadcastPacket(new S2C_LobbyChatMessage
        {
            SenderId = senderId,
            Sender = sender.Name,
            Message = message
        });
    }

    /// <summary>Broadcasts a system message (e.g., join/leave announcements).</summary>
    public void BroadcastSystem(string message)
    {
        transport.BroadcastPacket(new S2C_LobbyChatMessage
        {
            SenderId = Guid.Empty,
            Message = message,
            Sender = "<System>"
        });
    }
}
