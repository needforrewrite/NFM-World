﻿using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer.HttpMessages;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Game Master — dumb UDP relay for v1.
/// Validates join tokens, relays PlayerState between clients, handles race finish.
///
/// v2: <see cref="WorkerManager"/> and the Worker project will handle replay-based
/// validation. For now, the relay is direct.
/// </summary>
public class RaceOrchestrator : IDisposable
{
    private readonly ConcurrentDictionary<uint, ClientInfo> _clients = new();
    private readonly IMultiplayerServerTransport _transport;

    // join token → (session, player index)
    private readonly ConcurrentDictionary<Guid, (RaceSession Session, byte PlayerIndex, Guid ClientId)> _joinTokens = new();
    private readonly ConcurrentDictionary<uint, RaceSession> _sessions = new();

    // Server gamemode tick loop
    private CancellationTokenSource? _tickCts;
    private Thread? _tickThread;

    public RaceOrchestrator(IMultiplayerServerTransport transport)
    {
        _transport = transport;
        transport.PacketReceived += TransportOnPacketReceived;
        transport.ClientConnected += TransportOnClientConnected;
        transport.ClientDisconnected += TransportOnClientDisconnected;
        transport.ClientConnecting += TransportOnClientConnecting;
    }

    public void Start()
    {
        _transport.Start();
        StartServerTick();
    }

    public void Stop()
    {
        StopServerTick();
        _transport.Stop();
    }

    // ── Server gamemode tick loop (20 TPS) ────────────────────────────

    private void StartServerTick()
    {
        _tickCts = new CancellationTokenSource();
        _tickThread = new Thread(() =>
        {
            while (!_tickCts.Token.IsCancellationRequested)
            {
                try
                {
                    TickServerGamemodes();
                    Thread.Sleep(50); // 20 TPS
                }
                catch (Exception ex)
                {
                    Logging.Error($"[GM] Server tick error: {ex}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "GMServerTick"
        };
        _tickThread.Start();
    }

    private void StopServerTick()
    {
        _tickCts?.Cancel();
        _tickThread?.Join(TimeSpan.FromSeconds(1));
        _tickCts?.Dispose();
        _tickCts = null;
        _tickThread = null;
    }

    private void TickServerGamemodes()
    {
        foreach (var (_, session) in _sessions)
        {
            var gm = session.ServerGamemode;
            if (gm == null) continue;

            gm.GameTick();

            var snapshot = gm.GetStateSnapshot();
            if (!session.ResultsBroadcasted && snapshot is { IsFinished: true, Results: { } results })
            {
                // Broadcast final results as S2C_GameFinished
                session.ResultsBroadcasted = true;
                _transport.BroadcastPacket(
                    new S2C_GameFinished { Results = results },
                    reliable: true);
            }
        }
    }

    // ── Transport events ─────────────────────────────────────────────

    private void TransportOnClientConnecting(object? sender, uint clientIndex)
        => _clients.TryAdd(clientIndex, new ClientInfo { State = ClientState.Connecting });

    private void TransportOnClientConnected(object? sender, uint clientIndex)
    {
        if (_clients.TryGetValue(clientIndex, out var c))
            c.State = ClientState.Connected;
    }

    private void TransportOnClientDisconnected(object? sender, uint clientIndex)
        => _clients.TryRemove(clientIndex, out _); // TODO send disconnect packet in session

    // ── Packet dispatch ──────────────────────────────────────────────

    private void TransportOnPacketReceived(object? sender,
        (uint ClientIndex, IPacketClientToServer Packet) e)
    {
        switch (e.Packet)
        {
            case C2S_RaceLoaded raceLoaded:
                HandleRaceLoaded(e.ClientIndex, raceLoaded);
                break;
            case C2S_PlayerState playerState:
                HandlePlayerState(e.ClientIndex, playerState);
                break;
            case C2S_ClientEvent clientEvent:
                HandleClientEvent(e.ClientIndex, clientEvent);
                break;
        }
    }

    // ── Packet handlers ──────────────────────────────────────────────

    private void HandleClientEvent(uint clientIndex, C2S_ClientEvent clientEvent)
    {
        try
        {
            if (!TryFindSession(clientIndex, out var session, out var clientId)) return;
            session.ServerGamemode?.OnClientEvent(clientId, clientEvent.Payload.Span);
        }
        finally
        {
            clientEvent.Dispose();
        }
    }

    private void HandleRaceLoaded(uint clientIndex, C2S_RaceLoaded raceLoaded)
    {
        if (!_joinTokens.TryGetValue(raceLoaded.JoinToken, out var entry))
        {
            Logging.Warning($"[GM] Invalid join token from client {clientIndex}");
            return;
        }

        var clientId = entry.ClientId;

        entry.Session.Clients.TryAdd(clientIndex, clientId);
        entry.Session.LoadedCount++;

        Logging.Info(
            $"[GM] Client {clientId} loaded ({entry.Session.LoadedCount}/{entry.Session.PlayerCount})");

        if (entry.Session.LoadedCount >= entry.Session.PlayerCount)
        {
            entry.Session.ServerGamemode?.StartRace();
            _transport.SendPacketToClients(entry.Session.Clients.Keys.ToArray(), new S2C_RaceCanStart());
            Logging.Info($"[GM] Race starting: {entry.Session.PlayerCount} players");
        }
    }

    private void HandlePlayerState(uint clientIndex, C2S_PlayerState playerState)
    {
        if (!TryFindSession(clientIndex, out var session, out var clientId)) return;

        // Store latest position for server gamemode queries
        var pos = playerState.State.CarFrame.CarPosition;
        session.PlayerPositions[clientId] = new f64Vector3(pos.X, pos.Y, pos.Z);

        var others = session.Clients.Keys.Where(idx => idx != clientIndex).ToArray();
        if (others.Length > 0)
        {
            _transport.SendPacketToClients(others, new S2C_PlayerState
            {
                PlayerId = clientId,
                State = playerState.State,
                CurrentServerTime = DateTimeOffset.UtcNow
            }, false);
        }
    }

    // ── HTTP handler ─────────────────────────────────────────────────

    public Lobby2RaceServer_CreateRaceResponse CreateRace(Lobby2RaceServer_CreateRace raceParams)
    {
        var sessionId = Interlocked.Increment(ref _sessionIdCounter);
        var joinTokens = new Dictionary<byte, Guid>();

        var session = new RaceSession
        {
            Id = sessionId,
            PlayerCount = raceParams.MatchGameplayInfo.Players.Count,
            Clients = [],
            PlayerInfos = raceParams.MatchGameplayInfo.Players
        };

        foreach (var (playerIndex, playerInfo) in raceParams.MatchGameplayInfo.Players)
        {
            var token = Guid.NewGuid();
            joinTokens[playerIndex] = token;
            _joinTokens.TryAdd(token, (session, playerIndex, playerInfo.Id));
        }

        // Create server gamemode if a factory exists for this gamemode type
        var factory = new LuaGamemodeFactory(raceParams.MatchGameplayInfo.Gamemode, raceParams.MatchGameplayInfo.Parameters);
        if (factory.HasServerGamemode)
        {
            var context = new OrchestratorServerContext(
                session,
                raceParams.MatchGameplayInfo.StageName,
                payload => _transport.BroadcastPacket(new S2C_ServerEvent { Payload = payload }, reliable: true));

            // TODO this is a bit janky and could probably be improved
            var gm = factory.CreateServerGamemode(new GamemodeParameters
            {
                Players = raceParams.MatchGameplayInfo.Players
                    .Select(kvp => new ClientSidePlayerParameters
                    {
                        CarName = kvp.Value.Vehicle,
                        PlayerName = kvp.Value.Name,
                        IsBot = false,
                        IsClientPlayer = false,
                        Color = default
                    })
                    .ToList()
            }, context);
            
            session.ServerGamemode = gm;
            
            gm?.Begin();
        }

        _sessions.TryAdd(sessionId, session);

        Logging.Info(
            $"[GM] Race created: {raceParams.MatchKey}, {joinTokens.Count} players");

        return new Lobby2RaceServer_CreateRaceResponse
        {
            PlayerSecretIds = joinTokens
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private bool TryFindSession(uint clientIndex, [NotNullWhen(true)] out RaceSession? session, out Guid clientId)
    {
        foreach (var (_, s) in _sessions)
        {
            if (s.Clients.TryGetValue(clientIndex, out var id))
            {
                session = s;
                clientId = id;
                return true;
            }
        }

        session = null;
        clientId = Guid.Empty;
        return false;
    }

    public void Dispose()
    {
        StopServerTick();

        // Call End() on all server gamemodes before tearing down
        foreach (var (_, session) in _sessions)
        {
            session.ServerGamemode?.End();
        }

        _transport.PacketReceived -= TransportOnPacketReceived;
        _transport.ClientConnected -= TransportOnClientConnected;
        _transport.ClientDisconnected -= TransportOnClientDisconnected;
        _transport.ClientConnecting -= TransportOnClientConnecting;
    }

    private class ClientInfo
    {
        public ClientState State { get; set; }
    }

    private class RaceSession
    {
        public uint Id { get; set; }
        public int PlayerCount { get; set; }
        public int LoadedCount { get; set; }
        public ConcurrentDictionary<uint, Guid> Clients { get; set; } = [];
        public IServerGamemode? ServerGamemode { get; set; }
        public ConcurrentDictionary<Guid, f64Vector3> PlayerPositions { get; set; } = [];
        public IDictionary<byte, PlayerInfo> PlayerInfos { get; set; } = new Dictionary<byte, PlayerInfo>();
        public bool ResultsBroadcasted { get; set; }
    }

    /// <summary>
    /// Adapter that provides <see cref="IServerGamemodeData"/> from
    /// the RaceSession data available in the orchestrator.
    /// </summary>
    private class OrchestratorServerContext(
        RaceSession session,
        string stageName,
        Action<ReadOnlyMemory<byte>> broadcast) : IServerGamemodeData
    {
        public BackendStage CurrentStage { get; } = new(stageName);

        public IReadOnlyList<Guid> PlayerIds =>
            session.PlayerInfos.Values.Select(p => p.Id).ToList();

        public IReadOnlyDictionary<byte, PlayerInfo> PlayerInfos =>
            session.PlayerInfos.AsReadOnly();

        public f64Vector3? GetPlayerPosition(Guid playerId) =>
            session.PlayerPositions.TryGetValue(playerId, out var pos) ? pos : null;

        public void BroadcastEvent(ReadOnlyMemory<byte> payload) => broadcast(payload);
    }

    private static uint _sessionIdCounter;
}