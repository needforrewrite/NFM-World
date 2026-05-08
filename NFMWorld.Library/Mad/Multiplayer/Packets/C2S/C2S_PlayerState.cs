using MessagePack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[MessagePackObject]
public struct C2S_PlayerState : IPacketClientToServer<C2S_PlayerState>
{
    [Key(0)] public required PlayerState State;

    public C2S_PlayerState()
    {
    }
}