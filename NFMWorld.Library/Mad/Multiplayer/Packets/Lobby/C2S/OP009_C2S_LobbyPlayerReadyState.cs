using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

/// <summary>
/// Client → Lobby: toggle ready state in a session.
/// </summary>
[TightPackable]
[PacketClientToServer(9)]
public partial struct C2S_LobbyPlayerReadyState : IPacketClientToServer<C2S_LobbyPlayerReadyState>
{
    /// <summary>ID of the session the player is in.</summary>
    [TightPackOrder(0)]
    public required uint SessionId { get; set; }

    /// <summary>Whether the player is ready to start.</summary>
    [TightPackOrder(1)]
    public required bool IsReady { get; set; }
}
