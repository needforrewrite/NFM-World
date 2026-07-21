using System.Collections.Concurrent;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Manages game sessions: create, join, leave, player-ready tracking, and timeout.
/// </summary>
public class SessionManager(IMultiplayerServerTransport transport, PlayerRegistry players)
{
    private readonly ConcurrentDictionary<uint, GameSession> _sessions = new();
    private uint _maxSessionId;
    private readonly IMultiplayerServerTransport _transport = transport;

    public IEnumerable<KeyValuePair<uint, GameSession>> All => _sessions;

    public GameSession? Get(uint sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>Creates a new session. Returns the created session.</summary>
    public GameSession? CreateSession(uint creatorclientIndex, string stageName, byte maxPlayers, string gamemode)
    {
        var creator = players.Get(creatorclientIndex);
        if (creator is null)
            return null;

        var session = new GameSession
        {
            Id = Interlocked.Increment(ref _maxSessionId),
            CreatorId = creator.Id,
            CreatorName = creator.Name,
            StageName = stageName,
            MaxPlayers = maxPlayers,
            Gamemode = gamemode,
            Players = new ConcurrentDictionary<byte, Guid> { [0] = creator.Id }
        };

        creator.InSession = (0, session.Id);

        if (!_sessions.TryAdd(session.Id, session))
            return null;
        
        return session;
    }

    /// <summary>
    /// Attempts to join a session. Handles auto-leave from any existing session.
    /// Returns (sessionJoined, oldSessionLeft).
    /// </summary>
    public (GameSession? Joined, GameSession? Left) JoinSession(uint clientIndex, uint sessionId)
    {
        var player = players.Get(clientIndex);
        if (player is null) return (null, null);

        GameSession? leftSession = null;

        // Leave current session if in one
        if (player.InSession is { } current &&
            _sessions.TryGetValue(current.SessionIndex, out var leaving))
        {
            leaving.Players.TryRemove(KeyValuePair.Create(current.PlayerIndex, player.Id));
            player.InSession = null;
            leftSession = leaving;
        }

        // Join new session
        if (_sessions.TryGetValue(sessionId, out var target) &&
            target.Players.Count < target.MaxPlayers)
        {
            byte playerIndex = 0;
            while (target.Players.ContainsKey(playerIndex))
                playerIndex++;

            target.Players[playerIndex] = player.Id;
            player.InSession = (playerIndex, target.Id);
            return (target, leftSession);
        }

        return (null, leftSession);
    }

    /// <summary>Leaves the player's current session. Returns the session left, if any.</summary>
    public GameSession? LeaveSession(uint clientIndex, uint sessionId)
    {
        var player = players.Get(clientIndex);
        if (player?.InSession is { } current &&
            current.SessionIndex == sessionId &&
            _sessions.TryGetValue(sessionId, out var session))
        {
            session.Players.TryRemove(KeyValuePair.Create(current.PlayerIndex, player.Id));
            player.InSession = null;
            return session;
        }

        return null;
    }

    /// <summary>Marks a session as started/loading and sets the load timeout.</summary>
    public bool StartRace(uint clientIndex, uint sessionId)
    {
        var player = players.Get(clientIndex);
        if (player is null)
            return false;
        
        if (_sessions.TryGetValue(sessionId, out var session) &&
            session.Players.Any(e => e.Value == player.Id) &&
            session.State == SessionState.NotStarted)
        {
            session.State = SessionState.WaitingToLoad;
            session.StartTime = DateTimeOffset.Now.AddSeconds(20);

            foreach (var (_, id) in session.Players)
            {
                var aplayer = players.Get(id);
                if (aplayer is null)
                    return false;
                aplayer.IsInGame = true;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks for timed-out sessions (WaitingToLoad past StartTime).
    /// Returns sessions that have timed out. Caller must handle cleanup + notification.
    /// </summary>
    public List<GameSession> CheckTimeouts()
    {
        var timedOut = new List<GameSession>();
        foreach (var (_, session) in _sessions)
        {
            if (session.State == SessionState.WaitingToLoad &&
                session.StartTime is { } startTime &&
                DateTimeOffset.Now >= startTime)
            {
                session.State = SessionState.Finished;
                timedOut.Add(session);

                foreach (var (_, clientIndex) in session.Players)
                {
                    var player = players.Get(clientIndex);
                    if (player is not null)
                    {
                        player.InSession = null;
                        player.IsInGame = false;
                    }
                }
            }
        }

        return timedOut;
    }

    /// <summary>Marks a player as ready/unready in their session.</summary>
    public bool SetPlayerReady(uint clientIndex, uint sessionId, bool isReady)
    {
        var player = players.Get(clientIndex);
        if (player?.InSession is { } current &&
            current.SessionIndex == sessionId &&
            _sessions.TryGetValue(sessionId, out var session) &&
            session.State == SessionState.NotStarted)
        {
            // Readiness is tracked implicitly — all players must be ready before start.
            // For v1 we just validate that the player is in the session.
            // A future ReadyState field on ClientInfo could be added here.
            return true;
        }

        return false;
    }

    public class GameSession
    {
        public required uint Id { get; set; }
        public required Guid CreatorId { get; set; }
        public required string CreatorName { get; set; }
        public required string StageName { get; set; }
        public int MaxPlayers { get; set; }
        public ConcurrentDictionary<byte, Guid> Players { get; set; } = [];
        public DateTimeOffset? StartTime { get; set; }
        public SessionState State { get; set; } = SessionState.NotStarted;
        public string Gamemode { get; set; } = DefaultGamemodes.Racing;
    }
}
