using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

public interface IMultiplayerServerTransport
{
    IReadOnlyCollection<uint> Connections { get; }
    event EventHandler<(uint ClientIndex, IPacketClientToServer Packet)>? PacketReceived;
    event EventHandler<uint>? ClientConnecting;
    event EventHandler<uint>? ClientConnected;
    event EventHandler<uint>? ClientDisconnected;

    void SendPacketToClient<T>(uint clientIndex, T packet, bool reliable = true) where T : IPacketServerToClient<T>;

    /// <summary>
    /// Closes one client's connection. Used to evict a client whose credentials were refused,
    /// so it cannot keep sitting in the lobby as if it were admitted.
    /// </summary>
    void DisconnectClient(uint clientIndex);

    void SendPacketToClients<T>(ReadOnlySpan<uint> clientIndices, T packet, bool reliable = true)
        where T : IPacketServerToClient<T>;

    void BroadcastPacket<T>(T packet, bool reliable = true) where T : IPacketServerToClient<T>;

    void Stop();
    void Start();
}
