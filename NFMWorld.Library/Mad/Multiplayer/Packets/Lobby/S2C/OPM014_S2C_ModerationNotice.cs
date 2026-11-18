using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

/// <summary>
/// A moderation notice to one client: mute refusal, or a kick/ban before disconnect.
/// </summary>
[TightPackable]
[PacketServerToClient(-17)]
public partial struct S2C_ModerationNotice : IPacketServerToClient<S2C_ModerationNotice>
{
    [TightPackOrder(0)] public required string Message { get; set; }

    /// <summary>When true the client stops reconnecting and leaves the lobby after showing it.</summary>
    [TightPackOrder(1)]
    public bool Fatal { get; set; }
}
