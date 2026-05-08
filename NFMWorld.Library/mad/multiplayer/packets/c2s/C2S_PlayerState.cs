using MessagePack;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.c2s;

[MessagePackObject]
public struct C2S_PlayerState : IPacketClientToServer<C2S_PlayerState>
{
    [Key(0)] public required PlayerState State;

    public C2S_PlayerState()
    {
    }
}