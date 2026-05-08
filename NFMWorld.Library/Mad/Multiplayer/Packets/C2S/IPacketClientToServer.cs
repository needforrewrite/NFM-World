using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer.Packets.C2S;

public interface IPacketClientToServer : IPacket;

public interface IPacketClientToServer<out TSelf> : IPacketClientToServer, IReadableWritable<TSelf> where TSelf : IPacketClientToServer<TSelf>;