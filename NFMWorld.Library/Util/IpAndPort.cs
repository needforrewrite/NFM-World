using MemoryPack;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

[MemoryPackable]
public readonly partial record struct IpAndPort(CompactIpAddress Address, ushort Port);
