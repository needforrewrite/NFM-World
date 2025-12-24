using NFMWorld.Util;

namespace NFMWorld.Mad;

public interface IMultiplayerClientTransport
{
    ClientState State { get; }
    IPacketServerToClient[] GetNewPackets();
    void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>;
    void Stop();
}