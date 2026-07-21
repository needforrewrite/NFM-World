using System.Collections.Concurrent;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Tracks connected players: identity (name/vehicle/color), connection state,
/// session membership, and in-game status.
/// </summary>
public class PlayerRegistry
{
    private readonly Dictionary<uint, ClientInfo> _clients = new();
    private readonly Dictionary<Guid, ClientInfo> _clientsById = new();
    private readonly Lock _lock = new();

    public ClientInfo? Get(Guid id)
    {
        lock (_lock)
        {
            _clientsById.TryGetValue(id, out var client);
            return client;
        }
    }

    public ClientInfo GetOrAdd(uint clientIndex, ClientState state = ClientState.Connecting)
    {
        lock (_lock)
        {
            if (!_clients.TryGetValue(clientIndex, out var client))
            {
                client = new ClientInfo
                {
                    ClientIndex = clientIndex,
                    Id = Guid.NewGuid(),
                    State = state,
                };
                _clients[clientIndex] = client;
                _clientsById[client.Id] = client;
            }

            return client;
        }
    }

    public ClientInfo? Get(uint clientIndex)
    {
        lock (_lock)
        {
            _clients.TryGetValue(clientIndex, out var client);
            return client;
        }
    }

    public bool TryRemove(uint clientIndex, out ClientInfo? client)
    {
        lock (_lock)
        {
            if (_clients.Remove(clientIndex, out client))
            {
                _clientsById.Remove(client.Id);
                return true;
            }

            return false;
        }
    }

    public IEnumerable<KeyValuePair<uint, ClientInfo>> All
    {
        get
        {
            lock (_lock)
            {
                return _clients.ToArray();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _clients.Count;
            }
        }
    }

    /// <summary>
    /// Inner types must match the original GameOrchestrator inner classes
    /// for binary compatibility with live sessions.
    /// </summary>
    public class ClientInfo
    {
        public uint ClientIndex { get; set; }
        public Guid Id { get; set; }
        public ClientState State { get; set; }
        public string Name { get; set; } = "hogan rewish";
        public string Vehicle { get; set; } = "nfmm/radicalone";
        public Color3 Color { get; set; }
        public (byte PlayerIndex, uint SessionIndex)? InSession { get; set; }
        public bool IsInGame { get; set; }
    }
}
