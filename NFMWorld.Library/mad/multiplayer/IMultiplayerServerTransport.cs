using NFMWorldLibrary.Mad.Multiplayer.packets.c2s;
using NFMWorldLibrary.Mad.Multiplayer.packets.s2c;

namespace NFMWorldLibrary.Mad.Multiplayer;

public interface IMultiplayerServerTransport
{
    IReadOnlyCollection<uint> Connections { get; }
    event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
    event EventHandler<uint>? ClientConnecting;
    event EventHandler<uint>? ClientConnected;
    event EventHandler<uint>? ClientDisconnected;
    
    void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>;
    void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true) where T : IPacketServerToClient<T>;
    void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>;

    void Stop();
    void Start();
}