using System.Buffers;
using NFMWorld.Mad.packets;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IPacketClientToServer : IPacket;

public interface IPacketClientToServer<out TSelf> : IPacketClientToServer, IReadableWritable<TSelf> where TSelf : IPacketClientToServer<TSelf>;