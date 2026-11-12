using TightPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[TightPackable]
public readonly partial record struct IpAndPort(CompactIpAddress Address, ushort Port);
