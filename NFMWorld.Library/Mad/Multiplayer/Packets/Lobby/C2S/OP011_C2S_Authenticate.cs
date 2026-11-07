using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

/// <summary>
/// Presented by a client right after connecting, before any identity or session packet.
///
/// The lobby verifies the archive's signature and keeps the account id; without this it has
/// nothing but a self-asserted display name, so anyone could join under any name. A client
/// without an account sends an empty token and is admitted as a guest with no account id,
/// which keeps singleplayer and quick free play working while making ranked results
/// attributable.
/// </summary>
[MemoryPackable]
[PacketClientToServer(11)]
public partial struct C2S_Authenticate : IPacketClientToServer<C2S_Authenticate>
{
    /// <summary>
    /// Compact account token issued by the archive. Empty for a guest.
    /// </summary>
    [MemoryPackOrder(0)] public required string Token { get; set; }
}
