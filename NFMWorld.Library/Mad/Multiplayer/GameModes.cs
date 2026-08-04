using NFMWorld.Gameplay.Gamemodes;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes;

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

public class SandboxGamemodeFactory : BaseGamemodeFactory
{
    public override string GamemodeId => DefaultGamemodes.Sandbox;
    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new SandboxGamemode(parameters, gamemodeData);
}
public class TimeTrialGamemodeFactory : BaseGamemodeFactory
{
    public override string GamemodeId => "nfmm/timetrial";
    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new TimeTrialClientGamemode(parameters, gamemodeData);
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
        => new PvpClientGamemode(parameters, gamemodeData, constraint);

    public override IServerGamemode? CreateServerGamemode(GamemodeParameters parameters)
        => new PvpServerGamemode(constraint);
}
public class FootballGamemodeFactory : BaseGamemodeFactory
{
    public override string GamemodeId => DefaultGamemodes.Football;
    public override IGamemode CreateGameMode(GamemodeParameters parameters, IGamemodeData gamemodeData)
        => new FootballGamemode(parameters, gamemodeData);

    public override IServerGamemode? CreateServerGamemode(GamemodeParameters parameters)
        => new FootballServerGamemode();
}

/// <summary>
/// Factory for Lua-driven gamemodes. The gamemode ID is derived from the script path:
/// <c>nfmm/lua:{scriptRelativePath}</c>. Both client and server gamemodes are supported.
/// </summary>
public class LuaGamemodeFactory : BaseGamemodeFactory
{
    private readonly string _gamemodeId;
    private readonly string _scriptRelativePath;

    /// <summary>
    /// Creates a Lua gamemode factory.
    /// </summary>
    /// <param name="scriptRelativePath">
    /// Path to the .lua script, relative to <c>data/gamemodes/</c>.
    /// The gamemode ID is automatically derived as <c>nfmm/lua:{script}</c>.
    /// </param>
    public LuaGamemodeFactory(string scriptRelativePath)
    {
        _scriptRelativePath = scriptRelativePath;
        _gamemodeId = $"nfmm/lua:{scriptRelativePath}";
    }

    /// <summary>
    /// Creates a Lua gamemode factory with a custom gamemode ID.
    /// </summary>
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

public enum PvpConstraint
{
    Racing, Wasting, Both
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
        Register(new SandboxGamemodeFactory());
        Register(new TimeTrialGamemodeFactory());
        Register(new PvpGamemodeFactory(PvpConstraint.Racing));
        Register(new PvpGamemodeFactory(PvpConstraint.Wasting));
        Register(new PvpGamemodeFactory(PvpConstraint.Both));
        Register(new FootballGamemodeFactory());
    }

    public static void Register(BaseGamemodeFactory factory)
        => _factories[factory.GamemodeId] = factory;

    /// <summary>
    /// Registers a Lua-driven gamemode from a script file in <c>data/gamemodes/</c>.
    /// The gamemode ID is automatically derived as <c>nfmm/lua:{scriptRelativePath}</c>.
    /// </summary>
    /// <param name="scriptRelativePath">Path to the .lua file, relative to <c>data/gamemodes/</c>.</param>
    /// <returns>The registered factory.</returns>
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

    public static BaseGamemodeFactory? Get(string gamemodeId)
        => _factories.GetValueOrDefault(gamemodeId);
}