using NFMWorldLibrary.Multiplayer.packets.c2s;
using NFMWorldLibrary.Multiplayer.packets.s2c;

namespace NFMWorldLibrary.Multiplayer;

public interface IMultiplayerClientTransport
{
    ClientState State { get; }
    IPacketServerToClient[] GetNewPackets();
    void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>;
    void Stop();
}