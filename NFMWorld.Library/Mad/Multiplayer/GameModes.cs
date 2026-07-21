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

    public static BaseGamemodeFactory? Get(string gamemodeId)
        => _factories.GetValueOrDefault(gamemodeId);
}