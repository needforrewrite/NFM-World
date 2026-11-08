using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Sent once the lobby has checked the client's token against the archive. A rejected token
/// gets a refusal rather than a disconnect, so the client can fall back to guest or show a
/// "signed out" notice without losing its place in the lobby list.
/// </summary>
[MemoryPackable]
[PacketServerToClient(-9)]
public partial struct S2C_Authenticated : IPacketServerToClient<S2C_Authenticated>
{
    [MemoryPackOrder(0)] public required bool Accepted { get; set; }

    /// <summary>The archive account id, when accepted. Zero for a guest.</summary>
    [MemoryPackOrder(1)]
    public required int AccountId { get; set; }

    /// <summary>The account's username, so the lobby and other players see the account name rather than a claimed one.</summary>
    [MemoryPackOrder(2)]
    public required string Username { get; set; }

    [MemoryPackOrder(3)] public string? Error { get; set; }
}
