using System.Diagnostics.CodeAnalysis;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
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
    /// The config this factory was created with, or null if it was created without one.
    /// </summary>
    /// <remarks>
    /// A factory belongs to a single run: it owns whatever it loaded and releases it in
    /// <see cref="End"/>. Anything starting a second run - the <c>map</c> dev command
    /// changing stage mid-race, say - therefore has to build its own, and this is what it
    /// needs to build an equivalent one.
    /// </remarks>
    public virtual IReadOnlyDictionary<string, object>? Config => null;

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
            : throw new InvalidOperationException("Could not load gamemode plugin for " + gamemodeId);
    }

    [RequiresUnreferencedCode("Plugin loading requires loading code at runtime.")]
    public static BaseGamemodeFactory Create(string gamemodeId)
    {
        var plugin = LoadPlugin(gamemodeId);
        return plugin is not null
            ? new NativeGamemodeFactory(plugin)
            : throw new InvalidOperationException("Could not load gamemode plugin for " + gamemodeId);
    }

    [RequiresUnreferencedCode("Plugin loading requires loading code at runtime.")]
    private static GamemodePlugin? LoadPlugin(string gamemodeId)
        => GamemodePlugin.Load(gamemodeId, $"data/gamemodes/{gamemodeId}/gamemode.dll");
}
