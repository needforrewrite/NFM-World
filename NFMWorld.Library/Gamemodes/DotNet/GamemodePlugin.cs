using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Plugins;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// A gamemode loaded from a compiled plugin assembly
/// (<c>data/gamemodes/&lt;id&gt;/gamemode.dll</c>) rather than from Luau scripts.
/// </summary>
/// <remarks>
/// Loading, reflection and unloading are <see cref="PluginAssembly"/>'s; this adds the
/// gamemode contract's two constructors and its config schema.
/// </remarks>
public sealed class GamemodePlugin
{
    private static readonly Type[] ClientConstructorParameters =
        [typeof(ClientGamemodeParameters), typeof(IGamemodeContext), typeof(string), typeof(Dictionary<string, object>)];

    private static readonly Type[] ServerConstructorParameters =
        [typeof(ServerGamemodeParameters), typeof(IServerGamemodeContext), typeof(string), typeof(Dictionary<string, object>)];

    private readonly PluginAssembly _assembly;
    private ConstructorInfo? _clientConstructor;
    private ConstructorInfo? _serverConstructor;

    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    private GamemodePlugin(string id, string name, string description, PluginAssembly assembly)
    {
        Id = id;
        Name = name;
        Description = description;
        _assembly = assembly;
        ConfigProperties = ReadConfigProperties(assembly.Assembly);
        _clientConstructor = assembly.FindConstructor<PluginGamemode>(ClientConstructorParameters);
        _serverConstructor = assembly.TryFindConstructor<PluginServerGamemode>(ServerConstructorParameters);
    }

    /// <summary>Gamemode identifier, e.g. <c>nfmm/pvp</c>.</summary>
    public string Id { get; }

    /// <summary>Display name, from <c>[assembly: Gamemode]</c>.</summary>
    public string Name { get; }

    /// <summary>Description, from <c>[assembly: Gamemode]</c>.</summary>
    public string Description { get; }

    /// <summary>Config properties declared by the assembly's attributes.</summary>
    public IReadOnlyList<GamemodeConfigProperty> ConfigProperties { get; }

    /// <summary>Whether the assembly implements a server-side gamemode.</summary>
    public bool HasServerGamemode => _serverConstructor is not null;

    public Assembly Assembly => _assembly.Assembly;

    public bool IsAlive => _assembly.IsAlive;

    /// <summary>
    /// The reflected constructor, which keeps the plugin's assembly - and therefore
    /// its load context - reachable for as long as this plugin object is held. See
    /// <see cref="Unload"/>.
    /// </summary>
    private ConstructorInfo ClientConstructor
        => _clientConstructor ?? throw new InvalidOperationException($"Gamemode plugin '{Id}' has been unloaded.");

    /// <summary>
    /// Loads the plugin at <paramref name="path"/> into its own load context, or returns
    /// null if there is no assembly there or it is not marked <c>[assembly: Gamemode]</c>.
    /// </summary>
    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    public static GamemodePlugin? Load(string id, string path)
    {
        var assembly = PluginAssembly.Load(id, path);
        if (assembly is null)
            return null;

        if (assembly.Assembly.GetCustomAttribute<GamemodeAttribute>() is not { } attribute)
        {
            assembly.Unload();
            return null;
        }

        return new GamemodePlugin(id, attribute.Name, attribute.Description, assembly);
    }

    /// <summary>
    /// Creates the client-side gamemode instance.
    /// </summary>
    /// <remarks>
    /// The instance's type lives in this plugin's context, and anything that touches
    /// it - a field, a local, a collection - keeps the context loaded. Callers must
    /// therefore let go of it before <see cref="Unload"/> will do anything.
    /// </remarks>
    public PluginGamemode CreateClient(
        ClientGamemodeParameters parameters,
        IGamemodeContext context,
        Dictionary<string, object>? config = null)
        => (PluginGamemode)ClientConstructor.Invoke([parameters, context, Id, config]);

    /// <summary>Creates the server-side gamemode instance.</summary>
    public PluginServerGamemode CreateServer(
        ServerGamemodeParameters parameters,
        IServerGamemodeContext context,
        Dictionary<string, object>? config = null)
        => _serverConstructor is null
            ? throw new InvalidOperationException($"Gamemode '{Id}' has no server-side implementation.")
            : (PluginServerGamemode)_serverConstructor.Invoke([parameters, context, Id, config]);

    /// <summary>
    /// Detaches this plugin and asks its load context to unload. Returns immediately;
    /// the assembly is only collected once nothing references it any more.
    /// </summary>
    public void Unload()
    {
        if (_clientConstructor is null)
            return;

        // Drop the reflection handles last: they are the plugin object's own
        // remaining reference into the assembly.
        _clientConstructor = null;
        _serverConstructor = null;

        _assembly.Unload();
    }

    /// <summary>
    /// Reads the config schema an assembly declares with
    /// <see cref="GamemodeConfigPropertyAttribute"/> and its option attributes.
    /// </summary>
    private static List<GamemodeConfigProperty> ReadConfigProperties(Assembly assembly)
    {
        var properties = new List<GamemodeConfigProperty>();

        foreach (var attribute in assembly.GetCustomAttributes<GamemodeConfigPropertyAttribute>())
        {
            properties.Add(new GamemodeConfigProperty
            {
                Name = attribute.Name,
                Type = attribute.Type,
                Label = attribute.Label,
                Description = attribute.Description
            });
        }

        foreach (var option in assembly.GetCustomAttributes<GamemodeConfigPropertyOptionAttribute>())
        {
            if (properties.FirstOrDefault(p => p.Name == option.Property) is { } property)
            {
                property.Options.Add(new GamemodeConfigPropertyOption
                {
                    Label = option.Label,
                    Value = option.Value
                });
            }
        }

        return properties;
    }
}

/// <summary>A config property a plugin assembly declares.</summary>
public sealed class GamemodeConfigProperty
{
    public required string Name { get; init; }
    public required Type Type { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public List<GamemodeConfigPropertyOption> Options { get; } = [];
}

/// <summary>An allowed value for a plugin config property.</summary>
public sealed class GamemodeConfigPropertyOption
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}
