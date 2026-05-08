using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.s2c;

public interface IPacketServerToClient : IPacket;

public interface IPacketServerToClient<out TSelf> : IPacketServerToClient, IReadableWritable<TSelf> where TSelf : IPacketServerToClient<TSelf>;