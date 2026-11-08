using System.Diagnostics.CodeAnalysis;
using MemoryPack;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

public interface IPacketServerToClient : IPacket;

public interface
    IPacketServerToClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TSelf> : IPacketServerToClient,
    IReadableWritable<TSelf>, IMemoryPackable<TSelf> where TSelf : IPacketServerToClient<TSelf>, IMemoryPackable<TSelf>;
