using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// Reports account verification. Refused connections are disconnected; only a definitively
/// rejected session should be removed from the client's account store.
/// </summary>
[TightPackable]
[PacketServerToClient(-9)]
public partial struct S2C_Authenticated : IPacketServerToClient<S2C_Authenticated>
{
    [TightPackOrder(0)] public required bool Accepted { get; set; }

    /// <summary>The verified archive account id when accepted.</summary>
    [TightPackOrder(1)]
    public required int AccountId { get; set; }

    /// <summary>The account's username, so the lobby and other players see the account name rather than a claimed one.</summary>
    [TightPackOrder(2)]
    public required string Username { get; set; }

    [TightPackOrder(3)] public string? Error { get; set; }

    [TightPackOrder(4)] public bool SessionRejected { get; set; }
}
