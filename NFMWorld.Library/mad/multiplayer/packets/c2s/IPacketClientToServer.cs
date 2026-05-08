using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.packets.c2s;

public interface IPacketClientToServer : IPacket;

public interface IPacketClientToServer<out TSelf> : IPacketClientToServer, IReadableWritable<TSelf> where TSelf : IPacketClientToServer<TSelf>;