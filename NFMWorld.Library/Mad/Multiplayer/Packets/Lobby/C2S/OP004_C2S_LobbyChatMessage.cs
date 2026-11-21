using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(4)]
public partial struct C2S_LobbyChatMessage : IPacketClientToServer<C2S_LobbyChatMessage>
{
    [TightPackOrder(0)] public required string Message { get; set; } = string.Empty;

    /// <summary>Zero for room chat; the viewed game's ID for match chat.</summary>
    [TightPackOrder(1)]
    public uint SessionId { get; set; }

    public C2S_LobbyChatMessage()
    {
    }
}
