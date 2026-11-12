using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

[TightPackable]
[PacketClientToServer(1)]
public partial struct C2S_CreateSession : IPacketClientToServer<C2S_CreateSession>
{
    [TightPackOrder(0)] public required string StageName { get; set; }
    [TightPackOrder(1)] public required int MaxPlayers { get; set; }
    [TightPackOrder(2)] public required string GameMode { get; set; }
    [TightPackOrder(3)] public required Dictionary<string, string> GameModeParameters { get; set; }

    /// <summary>Bots to fill the empty slots with when the race starts. Not part of the field size.</summary>
    [TightPackOrder(4)]
    public int Bots { get; set; }
}
