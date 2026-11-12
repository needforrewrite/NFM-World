using System.Diagnostics.CodeAnalysis;
using TightPack;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

public interface IPacketClientToServer : IPacket;

public interface
    IPacketClientToServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TSelf> : IPacketClientToServer,
    IReadableWritable<TSelf>, ITightPackable<TSelf> where TSelf : IPacketClientToServer<TSelf>, ITightPackable<TSelf>;
