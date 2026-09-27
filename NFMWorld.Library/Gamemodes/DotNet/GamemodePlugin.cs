using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// A gamemode loaded from a compiled plugin assembly
/// (<c>data/gamemodes/&lt;id&gt;/gamemode.dll</c>) rather than from Luau scripts.
/// </summary>
/// <remarks>
/// Loaded into its own collectible <see cref="AssemblyLoadContext"/>, so a gamemode's
/// assembly is only resident while something is using it. See <see cref="Unload"/>.
/// </remarks>
public sealed class GamemodePlugin
{
    private const BindingFlags PublicInstance =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Type[] ClientConstructorParameters =
        [typeof(ClientGamemodeParameters), typeof(IGamemodeContext), typeof(string), typeof(Dictionary<string, object>)];

    private static readonly Type[] ServerConstructorParameters =
        [typeof(ServerGamemodeParameters), typeof(IServerGamemodeContext), typeof(string), typeof(Dictionary<string, object>)];

    private readonly PluginLoadContext _loadContext;
    private ConstructorInfo? _clientConstructor;
    private ConstructorInfo? _serverConstructor;
    private bool _unloaded;
    private readonly WeakReference _assemblyRef;

    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    private GamemodePlugin(
        string id,
        string name,
        string description,
        Assembly assembly,
        PluginLoadContext loadContext)
    {
        Id = id;
        Name = name;
        Description = description;
        Assembly = assembly;
        ConfigProperties = ReadConfigProperties(assembly);
        _loadContext = loadContext;
        _clientConstructor = FindConstructor<PluginGamemode>(assembly, id, ClientConstructorParameters);
        _serverConstructor = FindConstructor<PluginServerGamemode>(assembly, id, ServerConstructorParameters);
        _assemblyRef = new WeakReference(Assembly);
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

    public Assembly Assembly { get; private set; }

    /// <summary>
    /// The reflected constructor, which keeps the plugin's assembly - and therefore
    /// its load context - reachable for as long as this plugin object is held. See
    /// <see cref="Unload"/>.
    /// </summary>
    private ConstructorInfo ClientConstructor
        => _clientConstructor ?? throw new InvalidOperationException($"Gamemode plugin '{Id}' has been unloaded.");

    public bool IsAlive => _assemblyRef.IsAlive;

    /// <summary>
    /// Loads the plugin at <paramref name="path"/> into its own collectible context,
    /// or returns null if there is no assembly there or it is not marked
    /// <c>[assembly: Gamemode]</c>.
    /// </summary>
    /// <remarks>
    /// Read into memory and loaded from bytes rather than <c>Assembly.LoadFrom</c>:
    /// a load-from context resolves the assembly's dependencies relative to the file
    /// it was loaded from, which is the gamemode folder - the one place the shared
    /// assemblies deliberately are not.
    /// </remarks>
    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    public static GamemodePlugin? Load(string id, string path)
    {
        if (!VFS.FileExists(path))
            return null;

        using var stream = VFS.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var loadContext = new PluginLoadContext($"Gamemode:{id}");
        var assembly = loadContext.LoadFromStream(buffer);

        if (assembly.GetCustomAttribute<GamemodeAttribute>() is not { } attribute)
        {
            loadContext.Unload();
            return null;
        }

        return new GamemodePlugin(
            id,
            attribute.Name,
            attribute.Description,
            assembly,
            loadContext);
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
    /// <remarks>
    /// Plugins declare their own MemoryPack event types, and the generated formatters
    /// register themselves into MemoryPack's static table from the plugin's module
    /// initializer - so the table holds them from the moment the plugin loads. It lives
    /// in the host's MemoryPack assembly, so it holds the plugin's types, and through
    /// them the whole context, and would keep the gamemode resident for the lifetime of
    /// the process no matter what this method does. Dropping this plugin's entries is
    /// what actually makes the unload possible, and it has to happen while the types are
    /// still reachable through the reflection handles this object holds.
    /// </remarks>
    public void Unload()
    {
        if (_unloaded)
            return;

        _unloaded = true;

        var dropped = MemoryPackFormatterRegistry.DropFormatters(ClientConstructor.DeclaringType!.Assembly);

        // Drop the reflection handles last: they are the plugin object's own
        // remaining reference into the assembly.
        _clientConstructor = null;
        _serverConstructor = null;
        Assembly = null!;

        _loadContext.Unload();

        Logging.Info($"Unloading gamemode plugin '{Id}': {dropped} memorypack formatter(s) dropped");
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

    /// <summary>
    /// Finds the constructor of the single public subtype of
    /// <typeparamref name="T"/> the assembly declares. A gamemode declares one client
    /// and at most one server gamemode, so a second of either is a packaging mistake
    /// rather than something to guess between.
    /// </summary>
    [RequiresUnreferencedCode("Reflects over the assembly to find a constructor.")]
    private static ConstructorInfo FindConstructor<T>(Assembly assembly, string id, Type[] parameters)
    {
        var types = assembly.GetExportedTypes()
            .Where(t => !t.IsAbstract && typeof(T).IsAssignableFrom(t))
            .ToArray();

        if (types.Length != 1)
        {
            throw new InvalidOperationException(
                $"Gamemode assembly '{assembly.GetName().Name}' must declare exactly one {typeof(T).Name} " +
                $"subtype, but declares {types.Length}.");
        }

        return types[0].GetConstructor(PublicInstance, null, parameters, null)
               ?? throw new InvalidOperationException(
                   $"Gamemode '{id}': {types[0].FullName} does not have the expected constructor.");
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
