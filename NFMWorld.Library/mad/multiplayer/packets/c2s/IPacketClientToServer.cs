using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Mad.Multiplayer.packets.c2s;

public interface IPacketClientToServer : IPacket;

public interface IPacketClientToServer<out TSelf> : IPacketClientToServer, IReadableWritable<TSelf> where TSelf : IPacketClientToServer<TSelf>;