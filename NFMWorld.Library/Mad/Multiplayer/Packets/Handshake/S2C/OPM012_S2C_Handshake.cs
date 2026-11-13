using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Answers <see cref="C2S.C2S_Handshake"/>. Sent to every connection, accepted or not, so a
/// client can say why it was refused instead of retrying against a server that will keep
/// refusing it.
/// </summary>
[TightPackable]
[PacketServerToClient(Protocol.ServerHandshakeOpcode)]
public partial struct S2C_Handshake : IPacketServerToClient<S2C_Handshake>
{
    /// <summary>This server's wire version. Present on a refusal too, so the client can say which.</summary>
    [TightPackOrder(0)]
    public required int Version { get; set; }

    [TightPackOrder(1)] public required bool Accepted { get; set; }

    [TightPackOrder(2)] public string? Error { get; set; }
}
