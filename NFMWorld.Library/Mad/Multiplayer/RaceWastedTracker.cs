namespace NFMWorldLibrary.Multiplayer;

/// <summary>Orders owner snapshots and detects one wasted transition per car life.</summary>
public sealed class RaceWastedTracker
{
    private readonly Dictionary<Guid, (uint Tick, bool Wasted)> _states = [];

    public bool TryUpdate(Guid player, uint tick, bool wasted, out bool newlyWasted)
    {
        newlyWasted = false;

        if (_states.TryGetValue(player, out var previous) && tick <= previous.Tick)

            return false;
        newlyWasted = wasted && !previous.Wasted;
        _states[player] = (tick, wasted);

        return true;
    }

    public static string? Message(IEnumerable<KeyValuePair<byte, ServerSidePlayerInfo>> players, Guid victimId, int damageSource)
    {
        var roster = players.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
        var victim = Array.FindIndex(roster, p => p.Id == victimId);

        if (victim < 0)

            return null;
        var attacker = damageSource - 1;

        return attacker >= 0 && attacker < roster.Length && attacker != victim
            ? $"{roster[victim].PlayerName} has been wasted by {roster[attacker].PlayerName}!"
            : $"{roster[victim].PlayerName} has been wasted!";
    }
}
