using System.Collections.Concurrent;
using NFMWorldLibrary.Gamemodes;
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
    /// Bots that reserve lobby slots alongside the creator.
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

        lock (_lock)
        {
            // Creating a second session must not leave the first one behind holding the creator
            // alone: nobody else could ever start it, and it stays in the list for ever.
            if (creator.InSession is { } current && _sessions.TryGetValue(current.SessionIndex, out var previous))
                RemovePlayer(previous, creator);

            maxPlayers = (byte)Math.Clamp((int)maxPlayers, 2, MatchGameplayInfo.MaxPlayers);

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
                BotCount = gamemode == DefaultGamemodes.TimeTrial ? 0 : Math.Clamp(bots, 0, maxPlayers - 1)
            };
            FillBots(session);

            creator.InSession = (0, session.Id);

            if (!_sessions.TryAdd(session.Id, session))
                return null;

            return session;
        }
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
            // Already in it: re-leaving would end a session that only this player is in.
            if (player.InSession is { } already && already.SessionIndex == sessionId)
                return (_sessions.TryGetValue(sessionId, out var joinedCurrent) ? joinedCurrent : null, null);

            GameSession? leftSession = null;

            // Leave current session if in one
            if (player.InSession is { } current && _sessions.TryGetValue(current.SessionIndex, out var leaving))
            {
                RemovePlayer(leaving, player);
                leftSession = leaving;
            }

            // Join new session
            if (_sessions.TryGetValue(sessionId, out var target)
                && target.State == SessionState.NotStarted
                && target.Players.Count + target.Bots.Count < target.MaxPlayers)
            {
                byte playerIndex = 0;
                while (target.Players.ContainsKey(playerIndex) || target.Bots.ContainsKey(playerIndex))
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
        if (player is null)
            return null;

        lock (_lock)
        {
            if (player.InSession is not { } current
                || current.SessionIndex != sessionId
                || !_sessions.TryGetValue(sessionId, out var session))
                return null;

            RemovePlayer(session, player);
            return session;
        }
    }

    /// <summary>
    /// Takes a player out of whichever session it is in, as its connection going away does.
    /// Returns the session left, or null when it was not in one.
    /// </summary>
    public GameSession? RemovePlayer(PlayerRegistry.ClientInfo player)
    {
        lock (_lock)
        {
            if (player.InSession is not { } current || !_sessions.TryGetValue(current.SessionIndex, out var session))
                return null;

            RemovePlayer(session, player);
            return session;
        }
    }

    /// <summary>
    /// Removes a player from a session and keeps the session in a state someone can actually
    /// start. The caller holds <see cref="_lock"/>.
    /// </summary>
    private void RemovePlayer(GameSession session, PlayerRegistry.ClientInfo player)
    {
        if (player.InSession is { } current && current.SessionIndex == session.Id)
        {
            session.Players.TryRemove(KeyValuePair.Create(current.PlayerIndex, player.Id));
            player.InSession = null;
        }

        // A session nobody is in can never start and nobody can join it: it is dropped by the
        // pruner once it is marked finished.
        if (session.Players.IsEmpty)
        {
            // The race result can arrive after the last client goes back to the lobby.
            if (session.State == SessionState.Started)
            {
                session.ClosesAtUnixMs = DateTimeOffset.UtcNow.AddSeconds(FinishedLingerSeconds)
                    .ToUnixTimeMilliseconds();
                return;
            }

            session.State = SessionState.Finished;
            // A finished race with placings is still a thing to read. Its existing deadline
            // stands; one that was emptied before finishing gets no linger window.
            if (session.ClosesAtUnixMs == 0 && session.FinalStandings.Length > 0)
            {
                session.ClosesAtUnixMs = DateTimeOffset.UtcNow
                    .AddSeconds(FinishedLingerSeconds)
                    .ToUnixTimeMilliseconds();
            }

            return;
        }

        // Only the creator may start a race, and the client hides START for everyone else - so
        // a session that loses its creator while others remain is a dead end that sits in the
        // list for ever. Hand it to the next human instead. This also covers a race that is
        // about to fail and be put back to NotStarted, and a running one keeps a live owner.
        if (session.Players.Values.Contains(session.CreatorId))
            return;

        var successorId = session.Players.OrderBy(slot => slot.Key).First().Value;
        var successor = players.Get(successorId);
        if (successor is null)
            return;

        session.CreatorId = successor.Id;
        session.CreatorName = successor.Name;

        // The client that fielded a bot is the one that simulates it, so the bots follow the
        // creator. Left on the departed id they would be owned by nobody and never move.
        foreach (var slot in session.Bots.Keys.ToArray())
            session.Bots[slot] = session.Bots[slot] with { OwnerId = successor.Id };
    }

    public bool SetGameRule(uint clientIndex, uint sessionId, string name, string value)
    {
        lock (_lock)
        {
            if (players.Get(clientIndex) is not { Banned: false } host
                || host.InSession?.SessionIndex != sessionId
                || !_sessions.TryGetValue(sessionId, out var session)
                || session.CreatorId != host.Id || session.State != SessionState.NotStarted
                || name is null || value is null
                || !session.GamemodeParameters.TryGetValue(name, out var current) || current == value)
                return false;

            // Lobby snapshots can read the old dictionary while the rule is being updated.
            session.GamemodeParameters = new(session.GamemodeParameters) { [name] = value };
            return true;
        }
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
    /// Reserves bots within the session's total field size.
    /// </summary>
    private static void FillBots(GameSession session)
        => session.Bots = SessionBots.Place(session.Players.Keys, session.BotCount, session.CreatorId, session.MaxPlayers);

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
                // A load timeout has no placings to read; it is dropped on the next prune.
                session.ClosesAtUnixMs = 0;
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
    public bool PruneFinished()
    {
        var removed = false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var (id, session) in _sessions)
        {
            var empty = session.State == SessionState.NotStarted && session.Players.IsEmpty;
            // A finished race stays viewable long enough to read the placings. No deadline
            // means it was emptied rather than finished, so it goes now.
            var expired = session.State == SessionState.Finished
                          && (session.ClosesAtUnixMs == 0 || session.ClosesAtUnixMs <= now);
            var abandoned = session.State == SessionState.Started && session.Players.IsEmpty
                            && session.ClosesAtUnixMs > 0 && session.ClosesAtUnixMs <= now;
            if (!empty && !expired && !abandoned)
                continue;

            foreach (var playerId in session.Players.Values)
            {
                var player = players.Get(playerId);
                if (player?.InSession?.SessionIndex != id)
                    continue;
                player.InSession = null;
                player.IsInGame = false;
            }

            removed |= _sessions.TryRemove(id, out _);
        }

        return removed;
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

    /// <summary>How long a finished race stays listed with its placings.</summary>
    public const int FinishedLingerSeconds = 60;

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

        /// <summary>Configured number of reserved bot slots.</summary>
        public int BotCount { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        /// <summary>Unix ms at which this finished race is dropped; zero means it is not lingering.</summary>
        public long ClosesAtUnixMs { get; set; }

        /// <summary>The final order reported by the game master; empty if none was reported.</summary>
        public SessionStanding[] FinalStandings { get; set; } = [];

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
