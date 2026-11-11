using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Reports account verification. Refused connections are disconnected; only a definitively
/// rejected session should be removed from the client's account store.
/// </summary>
[MemoryPackable]
[PacketServerToClient(-9)]
public partial struct S2C_Authenticated : IPacketServerToClient<S2C_Authenticated>
{
    [MemoryPackOrder(0)] public required bool Accepted { get; set; }

    /// <summary>The verified archive account id when accepted.</summary>
    [MemoryPackOrder(1)]
    public required int AccountId { get; set; }

    /// <summary>The account's username, so the lobby and other players see the account name rather than a claimed one.</summary>
    [MemoryPackOrder(2)]
    public required string Username { get; set; }

    [MemoryPackOrder(3)] public string? Error { get; set; }

    [MemoryPackOrder(4)] public bool SessionRejected { get; set; }
}
