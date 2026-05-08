using MessagePack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MessagePackObject]
public struct C2S_LobbyChatMessage : IPacketClientToServer<C2S_LobbyChatMessage>
{
    [Key(0)] public required string Message { get; set; } = string.Empty;

    public C2S_LobbyChatMessage()
    {
    }
}