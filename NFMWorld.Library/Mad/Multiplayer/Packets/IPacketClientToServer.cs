using System.Diagnostics.CodeAnalysis;
using MemoryPack;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

public interface IPacketClientToServer : IPacket;

public interface
    IPacketClientToServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TSelf> : IPacketClientToServer,
    IReadableWritable<TSelf>, IMemoryPackable<TSelf> where TSelf : IPacketClientToServer<TSelf>, IMemoryPackable<TSelf>;
