using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

/// <summary>
/// Declares the sender's wire version. It is the first packet on every connection, ahead of
/// <see cref="C2S_Authenticate"/> or <see cref="C2S_RaceLoaded"/>: a packet layout from another
/// revision is not something the server can retry, so the link is checked before anything else
/// is exchanged.
/// </summary>
[TightPackable]
[PacketClientToServer(Protocol.ClientHandshakeOpcode)]
public partial struct C2S_Handshake : IPacketClientToServer<C2S_Handshake>
{
    [TightPackOrder(0)] public required int Version { get; set; }
}
