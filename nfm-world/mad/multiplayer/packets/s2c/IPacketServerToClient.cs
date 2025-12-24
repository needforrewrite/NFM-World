using System.Buffers;
using NFMWorld.Mad.packets;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IPacketServerToClient : IPacket;

public interface IPacketServerToClient<out TSelf> : IPacketServerToClient, IReadableWritable<TSelf> where TSelf : IPacketServerToClient<TSelf>;