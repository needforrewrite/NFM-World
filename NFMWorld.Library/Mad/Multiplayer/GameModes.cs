using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;
using Lua;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Gamemodes.Lua;
using NFMWorldLibrary.Radpack;

namespace NFMWorldLibrary.Multiplayer;

public abstract class BaseGamemodeFactory
{
    public abstract IGamemode CreateGameMode(ClientGamemodeParameters parameters, IGamemodeContext gamemodeContext);

    /// <summary>
    /// Creates a server-side gamemode for this factory's gamemode type.
    /// Returns null if this gamemode has no server-side logic (e.g., singleplayer-only).
    /// </summary>
    public virtual IServerGamemode? CreateServerGamemode(ServerGamemodeParameters parameters, IServerGamemodeContext context)
        => null;

    /// <summary>
    /// The gamemode identifier (e.g., "nfmm/racing") that this factory handles.
    /// Used by server-side gamemode lookup.
    /// </summary>
    public abstract string GamemodeId { get; }

    public abstract bool HasServerGamemode { get; }

    /// <summary>
    /// Called when the factory's race session is over. Releases anything the factory
    /// loaded for the run; a no-op for factories that hold nothing.
    /// </summary>
    public virtual void End()
    {
    }
}

public static class GamemodeFactory
{
    /// <summary>
    /// Creates the factory for a gamemode id, preferring a compiled plugin
    /// (<c>data/gamemodes/{id}/gamemode.dll</c>) over the Luau scripts of the same id.
    /// </summary>
    /// <remarks>
    /// The plugin is loaded fresh, not cached, so that it is only resident while a
    /// race using it is. The cost is re-reading and reflecting the assembly once per
    /// race, which is small next to the load it avoids. Callers must call
    /// <see cref="BaseGamemodeFactory.End"/> when the race is over.
    /// </remarks>
    [RequiresUnreferencedCode("Plugin loading requires loading code at runtime.")]
    public static BaseGamemodeFactory Create(string gamemodeId, IReadOnlyDictionary<string, object> config)
    {
        var plugin = LoadPlugin(gamemodeId);
        return plugin is not null
            ? new NativeGamemodeFactory(plugin, new Dictionary<string, object>(config))
            : new LuaGamemodeFactory(gamemodeId, config);
    }

    [RequiresUnreferencedCode("Plugin loading requires loading code at runtime.")]
    public static BaseGamemodeFactory Create(string gamemodeId)
    {
        var plugin = LoadPlugin(gamemodeId);
        return plugin is not null
            ? new NativeGamemodeFactory(plugin)
            : new LuaGamemodeFactory(gamemodeId);
    }

    [RequiresUnreferencedCode("Plugin loading requires loading code at runtime.")]
    private static GamemodePlugin? LoadPlugin(string gamemodeId)
        => GamemodePlugin.Load(gamemodeId, $"data/gamemodes/{gamemodeId}/gamemode.dll");
}

/// <summary>
/// Factory for Lua-driven gamemodes. Loads
/// <c>data/gamemodes/{scriptRelativePath}/client.lua</c> (and, once the server
/// framework lands, <c>server.lua</c>).
/// </summary>
public class LuaGamemodeFactory(string gamemodeId, LuaTable? config = null) : BaseGamemodeFactory
{
    private readonly RadpackLua? _radpack;

    public LuaGamemodeFactory(string gamemodeId, IReadOnlyDictionary<string, object> config) : this(gamemodeId, ToLuaTable(config))
    {
        var radpackPath = $"data/gamemodes/{gamemodeId}.radpack";
        if (VFS.FileExists(radpackPath))
        {
            var radpack = RadpackSerializer.Deserialize(VFS.ReadAllBytes(radpackPath));
            if (radpack is not RadpackLua lua)
            {
                throw new InvalidOperationException("Radpack does not contain a Lua Script Package");
            }
            
            if (!LuaGamemodeConfig.LoadConfig(lua).IsCompatible(config))
            {
                throw new InvalidOperationException("Provided gamemode config is not compatible with the gamemode.");
            }

            _radpack = lua;
        }
        else
        {
            if (!LuaGamemodeConfig.LoadConfig(gamemodeId).IsCompatible(config))
            {
                throw new InvalidOperationException("Provided gamemode config is not compatible with the gamemode.");
            }
        }
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
    public override bool HasServerGamemode => true;

    public override LuaGamemode CreateGameMode(ClientGamemodeParameters parameters, IGamemodeContext gamemodeContext)
    {
        return _radpack != null
            ? new LuaGamemode(parameters, gamemodeContext, gamemodeId, _radpack, config)
            : new LuaGamemode(parameters, gamemodeContext, gamemodeId, config);
    }

    public override LuaServerGamemode CreateServerGamemode(ServerGamemodeParameters parameters, IServerGamemodeContext context)
    {
        return _radpack != null
            ? new LuaServerGamemode(parameters, context, gamemodeId, _radpack, config)
            : new LuaServerGamemode(parameters, context, gamemodeId, config: config);
    }
}
