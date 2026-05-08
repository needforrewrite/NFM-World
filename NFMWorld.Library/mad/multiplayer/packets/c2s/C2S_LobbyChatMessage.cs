using MessagePack;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.c2s;

[MessagePackObject]
public struct C2S_LobbyChatMessage : IPacketClientToServer<C2S_LobbyChatMessage>
{
    [Key(0)] public required string Message { get; set; } = string.Empty;

    public C2S_LobbyChatMessage()
    {
    }
}