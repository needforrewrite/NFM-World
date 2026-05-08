using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.Packets.S2C;

public interface IPacketServerToClient : IPacket;

public interface IPacketServerToClient<out TSelf> : IPacketServerToClient, IReadableWritable<TSelf> where TSelf : IPacketServerToClient<TSelf>;