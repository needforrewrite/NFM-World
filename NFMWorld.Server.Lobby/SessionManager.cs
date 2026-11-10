using System.Collections.Concurrent;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// Manages game sessions: create, join, leave, player-ready tracking, and timeout.
/// </summary>
public class SessionManager(IMultiplayerServerTransport transport, PlayerRegistry players)
{
    private readonly ConcurrentDictionary<uint, GameSession> _sessions = new();
    private uint _maxSessionId;
    private readonly IMultiplayerServerTransport _transport = transport;
    private readonly Lock _lock = new();

    public IEnumerable<KeyValuePair<uint, GameSession>> All => _sessions;

    public GameSession? Get(uint sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>Creates a new session. Returns the created session.</summary>
    /// <param name="bots">
    /// Bots to place in the empty slots when the race starts. They are filled then, not now, so
    /// a human joining before the start takes one of those slots instead.
    /// </param>
    public GameSession? CreateSession(
        uint creatorclientIndex,
        string stageName,
        byte maxPlayers,
        string gamemode,
        Dictionary<string, string>? gamemodeParameters = null,
        int bots = 0
    )
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
            GamemodeParameters = gamemodeParameters is null ? [] : new(gamemodeParameters),
            Players = new ConcurrentDictionary<byte, Guid> { [0] = creator.Id },
            BotCount = Math.Max(0, bots)
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

        // Two joins arriving at once must not both take the same free slot.
        lock (_lock)
        {
            GameSession? leftSession = null;

            // Leave current session if in one
            if (player.InSession is { } current && _sessions.TryGetValue(current.SessionIndex, out var leaving))
            {
                leaving.Players.TryRemove(KeyValuePair.Create(current.PlayerIndex, player.Id));
                player.InSession = null;
                leftSession = leaving;
            }

            // Join new session
            if (_sessions.TryGetValue(sessionId, out var target) && target.Players.Count < target.MaxPlayers)
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
    }

    /// <summary>Leaves the player's current session. Returns the session left, if any.</summary>
    public GameSession? LeaveSession(uint clientIndex, uint sessionId)
    {
        var player = players.Get(clientIndex);
        if (player?.InSession is { } current
            && current.SessionIndex == sessionId
            && _sessions.TryGetValue(sessionId, out var session))
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

        // The state check and the transition must be atomic, or two members' start
        // packets both pass it and two races are created for one session.
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var session)
                && session.Players.Any(e => e.Value == player.Id)
                && session.State == SessionState.NotStarted)
            {
                session.State = SessionState.WaitingToLoad;
                session.StartTime = DateTimeOffset.UtcNow.AddSeconds(20);

                foreach (var (_, id) in session.Players)
                {
                    var aplayer = players.Get(id);
                    if (aplayer is null)
                        return false;
                    aplayer.IsInGame = true;
                }

                FillBots(session);

                // Past this point the load timeout no longer applies: the session is waiting for
                // the race to finish, not for players to load. Nothing ever cleared it, so every
                // race was marked timed out - and told it had failed - 20 seconds after starting.
                session.State = SessionState.Started;
                session.StartTime = null;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Places the session's bots in the free slots. Humans take slots first because they can
    /// join up to the moment the race starts, so this runs at the start, not at creation.
    /// </summary>
    private static void FillBots(GameSession session)
        => session.Bots = SessionBots.Place(session.Players.Keys, session.BotCount, session.CreatorId);

    /// <summary>
    /// Checks for timed-out sessions (WaitingToLoad past StartTime).
    /// Returns sessions that have timed out. Caller must handle cleanup + notification.
    /// </summary>
    public List<GameSession> CheckTimeouts()
    {
        var timedOut = new List<GameSession>();
        foreach (var (_, session) in _sessions)
        {
            // The deadline is absolute (StartTime = now + 20s), so it is reached once, not
            // every second after - which is why the state has to change when it fires.
            if (session.State == SessionState.WaitingToLoad
                && session.StartTime is { } startTime
                && DateTimeOffset.UtcNow >= startTime)
            {
                session.State = SessionState.Finished;
                timedOut.Add(session);

                // Players are keyed by slot to the player *id*, and IsInGame is tracked in the
                // registry by connection index. Looking up by id always missed, so every client
                // stayed flagged in-game and could not start anything again.
                foreach (var id in session.Players.Values)
                {
                    var player = players.Get(id);
                    if (player is null)
                        continue;
                    player.InSession = null;
                    player.IsInGame = false;
                }
            }
        }

        return timedOut;
    }

    /// <summary>
    /// Removes sessions that are over or empty so the lobby list does not grow forever.
    /// Only sessions in a terminal state are dropped; a race in progress stays visible.
    /// </summary>
    public void PruneFinished()
    {
        foreach (var (id, session) in _sessions)
        {
            var finished = session.State == SessionState.Finished;
            var empty = session.State == SessionState.NotStarted && session.Players.IsEmpty;
            if (finished || empty)
                _sessions.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Ends a session that nobody is in any more. Its state moves to Finished so the next
    /// <see cref="PruneFinished"/> drops it, and the lobby stops offering it as a race to watch.
    /// </summary>
    public void AbortSession(uint sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
                session.State = SessionState.Finished;
        }
    }

    /// <summary>Marks a player as ready/unready in their session.</summary>
    public bool SetPlayerReady(uint clientIndex, uint sessionId, bool isReady)
    {
        var player = players.Get(clientIndex);
        if (player?.InSession is { } current
            && current.SessionIndex == sessionId
            && _sessions.TryGetValue(sessionId, out var session)
            && session.State == SessionState.NotStarted)
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

        /// <summary>
        /// Slots filled by bots, by slot. Kept apart from <see cref="Players"/> because a bot has
        /// no connection: every path that maps a slot back to a client would otherwise have to
        /// special-case an id the registry does not know.
        /// </summary>
        public Dictionary<byte, ServerSidePlayerInfo> Bots { get; set; } = [];

        /// <summary>How many bots to place in the free slots when the race starts.</summary>
        public int BotCount { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public SessionState State { get; set; } = SessionState.NotStarted;
        public string Gamemode { get; set; } = DefaultGamemodes.PvP;
        public Dictionary<string, string> GamemodeParameters { get; set; } = [];

        /// <summary>Every occupied slot, humans and bots, in slot order.</summary>
        public IEnumerable<(byte Slot, Guid Id)> AllSlots()
            => Players.Select(kv => (kv.Key, kv.Value)).Concat(Bots.Keys.Select(b => (b, Guid.Empty)))
                .OrderBy(e => e.Item1);

        /// <summary>True when the slot is a bot rather than a connected human.</summary>
        public bool IsBot(byte slot) => Bots.ContainsKey(slot);
    }
}
