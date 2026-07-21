using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

/// <summary>
/// Client → Lobby: toggle ready state in a session.
/// </summary>
[MemoryPackable]
[PacketClientToServer(9)]
public partial struct C2S_LobbyPlayerReadyState : IPacketClientToServer<C2S_LobbyPlayerReadyState>
{
    /// <summary>ID of the session the player is in.</summary>
    [MemoryPackOrder(0)] public required uint SessionId { get; set; }

    /// <summary>Whether the player is ready to start.</summary>
    [MemoryPackOrder(1)] public required bool IsReady { get; set; }
}
