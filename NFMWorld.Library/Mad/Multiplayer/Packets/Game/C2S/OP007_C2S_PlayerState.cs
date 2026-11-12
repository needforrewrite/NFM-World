using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(7)]
public partial struct C2S_PlayerState : IPacketClientToServer<C2S_PlayerState>
{
    [TightPackOrder(0)] public required PlayerState State;

    /// <summary>
    /// Which player this state belongs to. A client may simulate more than its own car - the
    /// bots it owns - so the sender cannot be taken to mean the car. Empty means "the sender".
    /// </summary>
    [TightPackOrder(1)]
    public Guid PlayerId { get; set; }
}
