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

public class TimeTrialGamemodeFactory : BaseGamemodeFactory
{
    public override string GamemodeId => "nfmm/timetrial";
    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new LuaGamemode(parameters, gamemodeData, "timetrial");
}
public class TimeTrialPreviewGamemodeFactory(SavedTimeTrial timeTrial) : BaseGamemodeFactory
{
    public override string GamemodeId => "nfmm/timetrial-preview";
    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new TimeTrialPreviewGamemode(parameters, gamemodeData, timeTrial);
}
public class PvpGamemodeFactory(PvpConstraint constraint) : BaseGamemodeFactory
{
    public override string GamemodeId => constraint switch
    {
        PvpConstraint.Racing => DefaultGamemodes.Racing,
        PvpConstraint.Wasting => DefaultGamemodes.Wasting,
        PvpConstraint.Both => DefaultGamemodes.Both,
        _ => DefaultGamemodes.Racing
    };
    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new LuaGamemode(
            parameters,
            gamemodeData,
            "pvp",
            $"{{\"constraint\":\"{constraint.ToString().ToLowerInvariant()}\"}}");

    public override IServerGamemode? CreateServerGamemode(GamemodeParameters parameters)
        => new LuaServerGamemode(GamemodeId, "pvp");
}
public enum PvpConstraint
{
    Racing, Wasting, Both
}

/// <summary>
/// Factory for Lua-driven gamemodes. Loads
/// <c>data/gamemodes/{scriptRelativePath}/client.lua</c> (and, once the server
/// framework lands, <c>server.lua</c>).
/// </summary>
public class LuaGamemodeFactory : BaseGamemodeFactory
{
    private readonly string _gamemodeId;
    private readonly string _scriptRelativePath;

    public LuaGamemodeFactory(string scriptRelativePath)
    {
        _scriptRelativePath = scriptRelativePath;
        _gamemodeId = $"nfmm/lua:{scriptRelativePath}";
    }

    public LuaGamemodeFactory(string gamemodeId, string scriptRelativePath)
    {
        _gamemodeId = gamemodeId;
        _scriptRelativePath = scriptRelativePath;
    }

    public override string GamemodeId => _gamemodeId;

    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new LuaGamemode(parameters, gamemodeData, _scriptRelativePath);

    public override IServerGamemode? CreateServerGamemode(GamemodeParameters parameters)
        => new LuaServerGamemode(_gamemodeId, _scriptRelativePath);
}

/// <summary>
/// Registry mapping gamemode IDs (e.g., "nfmm/racing") to their factories.
/// Used by server-side code to create server gamemodes from string IDs.
/// </summary>
public static class GamemodeRegistry
{
    private static readonly Dictionary<string, BaseGamemodeFactory> _factories = new();

    static GamemodeRegistry()
    {
        Register(new TimeTrialGamemodeFactory());
        Register(new PvpGamemodeFactory(PvpConstraint.Racing));
        Register(new PvpGamemodeFactory(PvpConstraint.Wasting));
        Register(new PvpGamemodeFactory(PvpConstraint.Both));
    }

    public static void Register(BaseGamemodeFactory factory)
        => _factories[factory.GamemodeId] = factory;

    public static BaseGamemodeFactory? Get(string gamemodeId)
        => _factories.GetValueOrDefault(gamemodeId);

    /// <summary>
    /// Registers a Lua-driven gamemode from <c>data/gamemodes/{scriptRelativePath}</c>.
    /// The gamemode ID is derived as <c>nfmm/lua:{scriptRelativePath}</c>.
    /// </summary>
    public static LuaGamemodeFactory RegisterLua(string scriptRelativePath)
    {
        var factory = new LuaGamemodeFactory(scriptRelativePath);
        Register(factory);
        return factory;
    }

    /// <summary>
    /// Registers a Lua-driven gamemode with a custom gamemode ID.
    /// </summary>
    public static LuaGamemodeFactory RegisterLua(string gamemodeId, string scriptRelativePath)
    {
        var factory = new LuaGamemodeFactory(gamemodeId, scriptRelativePath);
        Register(factory);
        return factory;
    }
}