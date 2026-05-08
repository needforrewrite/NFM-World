using NFMWorldLibrary.Mad.Multiplayer.packets.c2s;
using NFMWorldLibrary.Mad.Multiplayer.packets.s2c;

namespace NFMWorldLibrary.Mad.Multiplayer;

public interface IMultiplayerClientTransport
{
    ClientState State { get; }
    IPacketServerToClient[] GetNewPackets();
    void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>;
    void Stop();
}