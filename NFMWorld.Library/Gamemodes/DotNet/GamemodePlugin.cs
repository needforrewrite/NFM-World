using System.Reflection;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// A gamemode loaded from a compiled plugin assembly
/// (<c>data/gamemodes/&lt;id&gt;/gamemode.dll</c>) rather than from Luau scripts.
/// </summary>
/// <remarks>
/// The assembly is expected to be built without copies of the assemblies the host
/// already ships (NFMWorld.Library, Lua, MemoryPack, ...) - the plugin projects
/// reference those with <c>Private="false" ExcludeAssets="runtime"</c> - so it is
/// loaded into the host's own load context and resolves against the host's copies
/// instead of loading a second, incompatible set.
/// </remarks>
public sealed class GamemodePlugin
{
    private const BindingFlags PublicInstance =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Type[] ClientConstructorParameters =
        [typeof(ClientGamemodeParameters), typeof(IGamemodeContext), typeof(string), typeof(Dictionary<string, object>)];

    private static readonly Type[] ServerConstructorParameters =
        [typeof(ServerGamemodeParameters), typeof(IServerGamemodeContext), typeof(string), typeof(Dictionary<string, object>)];

    private readonly ConstructorInfo _clientConstructor;
    private readonly ConstructorInfo? _serverConstructor;

    private GamemodePlugin(
        string id,
        string name,
        string description,
        IReadOnlyList<GamemodeConfigProperty> configProperties,
        ConstructorInfo clientConstructor,
        ConstructorInfo? serverConstructor)
    {
        Id = id;
        Name = name;
        Description = description;
        ConfigProperties = configProperties;
        _clientConstructor = clientConstructor;
        _serverConstructor = serverConstructor;
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

    /// <summary>
    /// Loads the plugin at <paramref name="path"/>, or returns null if there is no
    /// assembly there or it is not marked <c>[assembly: Gamemode]</c>.
    /// </summary>
    /// <remarks>
    /// Read into memory and loaded from bytes rather than <c>Assembly.LoadFrom</c>:
    /// a load-from context resolves the assembly's dependencies relative to the file
    /// it was loaded from, which is the gamemode folder - the one place the shared
    /// assemblies deliberately are not.
    /// </remarks>
    public static GamemodePlugin? Load(string id, string path)
    {
        if (!VFS.FileExists(path))
            return null;

        using var stream = VFS.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var assembly = Assembly.Load(buffer.ToArray());

        if (assembly.GetCustomAttribute<GamemodeAttribute>() is not { } attribute)
            return null;

        return new GamemodePlugin(
            id,
            attribute.Name,
            attribute.Description,
            ReadConfigProperties(assembly),
            FindConstructor<PluginGamemode>(assembly, id, ClientConstructorParameters),
            FindConstructor<PluginServerGamemode>(assembly, id, ServerConstructorParameters));
    }

    /// <summary>The concrete client gamemode type the assembly declares.</summary>
    public Type GetClientGamemodeType() => _clientConstructor.DeclaringType!;

    /// <summary>The concrete server gamemode type the assembly declares, if any.</summary>
    public Type? GetServerGamemodeType() => _serverConstructor?.DeclaringType;

    /// <summary>Creates the client-side gamemode instance.</summary>
    public PluginGamemode CreateClient(
        ClientGamemodeParameters parameters,
        IGamemodeContext context,
        Dictionary<string, object>? config = null)
        => (PluginGamemode)_clientConstructor.Invoke([parameters, context, Id, config]);

    /// <summary>Creates the server-side gamemode instance.</summary>
    public PluginServerGamemode CreateServer(
        ServerGamemodeParameters parameters,
        IServerGamemodeContext context,
        Dictionary<string, object>? config = null)
        => _serverConstructor is null
            ? throw new InvalidOperationException($"Gamemode '{Id}' has no server-side implementation.")
            : (PluginServerGamemode)_serverConstructor.Invoke([parameters, context, Id, config]);

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
