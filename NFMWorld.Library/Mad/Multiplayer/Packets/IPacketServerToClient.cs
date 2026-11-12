using System.Diagnostics.CodeAnalysis;
using TightPack;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

public interface IPacketServerToClient : IPacket;

public interface
    IPacketServerToClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TSelf> : IPacketServerToClient,
    IReadableWritable<TSelf>, ITightPackable<TSelf> where TSelf : IPacketServerToClient<TSelf>, ITightPackable<TSelf>;
