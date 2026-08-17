using Lua;
using NFMWorld.Gameplay.Gamemodes;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.Lua;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseGamemodeFactory
{
    public abstract IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData);

    /// <summary>
    /// Creates a server-side gamemode for this factory's gamemode type.
    /// Returns null if this gamemode has no server-side logic (e.g., singleplayer-only).
    /// </summary>
    public virtual IServerGamemode? CreateServerGamemode(GamemodeParameters parameters)
        => null;

    /// <summary>
    /// The gamemode identifier (e.g., "nfmm/racing") that this factory handles.
    /// Used by server-side gamemode lookup.
    /// </summary>
    public abstract string GamemodeId { get; }
}

/// <summary>
/// Factory for Lua-driven gamemodes. Loads
/// <c>data/gamemodes/{scriptRelativePath}/client.lua</c> (and, once the server
/// framework lands, <c>server.lua</c>).
/// </summary>
public class LuaGamemodeFactory(string gamemodeId, LuaTable? config = null) : BaseGamemodeFactory
{
    public LuaGamemodeFactory(string gamemodeId, IReadOnlyDictionary<string, object> config) : this(gamemodeId, ToLuaTable(config))
    {
    }

    private static LuaTable ToLuaTable(IReadOnlyDictionary<string, object> dict)
    {
        var table = new LuaTable();
        foreach (var (k, obj) in dict)
        {
            if (obj is LuaValue val) table[k] = val;
            else if (obj is string str) table[k] = str;
            else if (obj is bool b) table[k] = b;
            else if (obj is byte by) table[k] = by;
            else if (obj is sbyte sby) table[k] = sby;
            else if (obj is short s) table[k] = s;
            else if (obj is ushort u) table[k] = u;
            else if (obj is int i) table[k] = i;
            else if (obj is uint ui) table[k] = ui;
            else if (obj is long l) table[k] = l;
            else if (obj is ulong ul) table[k] = ul;
            else if (obj is float f) table[k] = f;
            else if (obj is double d) table[k] = d;
        }
        return table;
    }

    public override string GamemodeId => gamemodeId;

    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new LuaGamemode(parameters, gamemodeData, gamemodeId, config);

    public override IServerGamemode? CreateServerGamemode(GamemodeParameters parameters)
        => new LuaServerGamemode(gamemodeId, gamemodeId, config);
}
