using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

public interface IMultiplayerClientTransport
{
    ClientState State { get; }
    IPacketServerToClient[] GetNewPackets();
    void SendPacketToServer<T>(T packet, bool reliable = true) where T : IPacketClientToServer<T>;
    void Stop();

    /// <summary>
    /// True while the transport is trying to restore a dropped connection on its own.
    /// A transport that never retries simply reports <see cref="ClientState.Disconnected"/>.
    /// </summary>
    bool IsReconnecting => false;

    /// <summary>
    /// True once reconnection has given up. Distinct from disconnected: the player has to act.
    /// </summary>
    bool ReconnectFailed => false;

    /// <summary>Raised after a reconnection succeeded, so the caller can re-send its handshake.</summary>
    event Action? Reconnected
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Disables automatic reconnection. The caller uses this when the server evicted it for a
    /// reason a retry would only repeat, such as a refused token.
    /// </summary>
    void StopReconnecting()
    {
    }
}
