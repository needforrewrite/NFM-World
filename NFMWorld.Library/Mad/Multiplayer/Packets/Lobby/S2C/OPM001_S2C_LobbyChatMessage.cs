using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
[PacketServerToClient(-1)]
public partial struct S2C_LobbyChatMessage() : IPacketServerToClient<S2C_LobbyChatMessage>
{
    [TightPackOrder(0)] public required string Sender { get; set; } = string.Empty;
    [TightPackOrder(1)] public required Guid SenderId { get; set; }
    [TightPackOrder(2)] public required string Message { get; set; } = string.Empty;

    /// <summary>Zero for room chat; a session id for that game's match lobby chat.</summary>
    [TightPackOrder(3)]
    public uint SessionId { get; set; }

    /// <summary>The sender's colour. In-game chat arrives from a game master without a roster entry.</summary>
    [TightPackOrder(4)]
    public Color3 Color { get; set; }
}
