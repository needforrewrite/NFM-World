using Lua;
using Lua.Runtime;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Converts between Lua tables and C# gamemode result/snapshot types.
/// </summary>
internal static class LuaResultsConverter
{
    /// <summary>
    /// Converts a Lua table to a <see cref="RaceResults"/> struct.
    /// Expected Lua format:
    /// <code>
    /// {
    ///     standings = {
    ///         { player_id = "...", finish_position = 0, finish_time_ms = 12345, is_client_player = true },
    ///         ...
    ///     },
    ///     race_duration_ms = 123456,
    ///     gamemode_id = "nfmm/lua:..."
    /// }
    /// </code>
    /// </summary>
    public static RaceResults ToRaceResults(LuaValue luaValue)
    {
        if (luaValue.Type != LuaValueType.Table)
            throw new ArgumentException("Expected a Lua table for RaceResults.");

        var table = luaValue.Read<LuaTable>();

        // Standings.
        var standingsTable = table["standings"].Read<LuaTable>();
        var count = standingsTable.ArrayLength;
        var standings = new RaceStanding[count];
        for (int i = 0; i < count; i++)
        {
            var s = standingsTable[i + 1].Read<LuaTable>();
            standings[i] = new RaceStanding
            {
                PlayerId = Guid.Parse(s["player_id"].Read<string>()),
                FinishPosition = (int)s["finish_position"].Read<double>(),
                FinishTime = s["finish_time_ms"].Type == LuaValueType.Nil
                    ? null
                    : TimeSpan.FromMilliseconds(s["finish_time_ms"].Read<double>()),
                IsClientPlayer = s["is_client_player"].Type != LuaValueType.Nil
                                 && s["is_client_player"].Read<bool>(),
            };
        }

        return new RaceResults
        {
            Standings = standings,
            RaceDuration = TimeSpan.FromMilliseconds(table["race_duration_ms"].Read<double>()),
            GamemodeId = table["gamemode_id"].Read<string>(),
        };
    }

    /// <summary>
    /// Converts a <see cref="RaceResults"/> struct to a Lua table.
    /// </summary>
    public static LuaValue FromRaceResults(RaceResults results)
    {
        var table = new LuaTable(0, 3);

        var standingsArray = new LuaTable(results.Standings.Length, 0);
        for (int i = 0; i < results.Standings.Length; i++)
        {
            var s = results.Standings[i];
            var sTable = new LuaTable(0, 4);
            sTable["player_id"] = s.PlayerId.ToString();
            sTable["finish_position"] = (double)s.FinishPosition;
            sTable["finish_time_ms"] = s.FinishTime?.TotalMilliseconds ?? (LuaValue)LuaValue.Nil;
            sTable["is_client_player"] = s.IsClientPlayer;
            standingsArray[i + 1] = sTable;
        }
        table["standings"] = standingsArray;
        table["race_duration_ms"] = results.RaceDuration.TotalMilliseconds;
        table["gamemode_id"] = results.GamemodeId;

        return table;
    }

    /// <summary>
    /// Converts a Lua table to a <see cref="GameStateSnapshot"/> struct.
    /// Expected Lua format:
    /// <code>
    /// {
    ///     is_finished = false,
    ///     results = { ... }  or nil,
    ///     state = { key = value, ... }
    /// }
    /// </code>
    /// </summary>
    public static GameStateSnapshot? ToGameStateSnapshot(LuaValue luaValue)
    {
        if (luaValue.Type == LuaValueType.Nil)
            return null;

        var table = luaValue.Read<LuaTable>();

        var isFinished = table["is_finished"].Type != LuaValueType.Nil
                         && table["is_finished"].Read<bool>();

        RaceResults? results = null;
        if (table["results"].Type == LuaValueType.Table)
            results = ToRaceResults(table["results"]);

        Dictionary<string, object>? state = null;
        if (table["state"].Type == LuaValueType.Table)
        {
            var stateTable = table["state"].Read<LuaTable>();
            state = new Dictionary<string, object>();
            // Iterate string-keyed entries (LuaTable provides key-value pairs).
            foreach (var kv in stateTable)
            {
                // Only include string-keyed primitive values.
                if (kv.Key.Type == LuaValueType.String)
                {
                    var key = kv.Key.Read<string>();
                    state[key] = LuaValueToObject(kv.Value);
                }
            }
        }

        return new GameStateSnapshot
        {
            IsFinished = isFinished,
            Results = results,
            State = state,
        };
    }

    /// <summary>Converts a simple LuaValue to a CLR object for state dicts.</summary>
    private static object LuaValueToObject(LuaValue val) => val.Type switch
    {
        LuaValueType.Nil => null!,
        LuaValueType.Boolean => (object)val.Read<bool>(),
        LuaValueType.Number => val.Read<double>(),
        LuaValueType.String => val.Read<string>(),
        _ => val.ToString() ?? val.Type.ToString(),
    };
}
