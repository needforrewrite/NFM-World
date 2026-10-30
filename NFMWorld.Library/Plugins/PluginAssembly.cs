using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NFMWorldLibrary.Backend;

namespace NFMWorldLibrary.Plugins;

internal static class PluginPaths
{
    /// <summary>
    /// Where a plugin kind's assemblies live. Each kind keeps its own folder so the
    /// file name can stay identical (<c>gamemode.dll</c>, <c>ai.dll</c>) and the folder
    /// disambiguates - <see cref="PluginAssembly.Id"/> is that folder.
    /// </summary>
    public static string For(string kind, string id, string fileName)
        => $"data/{kind}/{id}/{fileName}";
}

/// <summary>
/// A plugin assembly loaded from <c>data/&lt;kind&gt;/&lt;id&gt;/&lt;file&gt;.dll</c> into
/// its own collectible <see cref="PluginLoadContext"/>.
/// </summary>
/// <remarks>
/// Shared by every plugin kind (gamemode, AI, ...): the loading, reflection and
/// unloading rules are identical and subtly wrong in ways that are expensive to
/// rediscover, so they live here once. A kind supplies its own attribute reader and
/// constructor signature.
///
/// The assembly is only resident while something is using it. See <see cref="Unload"/>.
/// </remarks>
public sealed class PluginAssembly
{
    /// <summary>
    /// Loaded plugins on a runtime with no collectible contexts, keyed by plugin id.
    /// There the assembly can never be released, so it is loaded once and reused rather
    /// than re-read per run.
    /// </summary>
    private static readonly Dictionary<string, (PluginLoadContext Context, Assembly Assembly)> Resident = [];

    private readonly PluginLoadContext _loadContext;
    private readonly WeakReference _assemblyRef;
    private bool _unloaded;

    private PluginAssembly(string id, string path, Assembly assembly, PluginLoadContext loadContext)
    {
        Id = id;
        Path = path;
        Assembly = assembly;
        _loadContext = loadContext;
        _assemblyRef = new WeakReference(assembly);
    }

    /// <summary>Plugin identifier, e.g. <c>nfmm/pvp</c>.</summary>
    public string Id { get; }

    /// <summary>Path the plugin was loaded from, e.g. <c>data/gamemodes/nfmm/pvp/gamemode.dll</c>.</summary>
    public string Path { get; }

    public Assembly Assembly { get; private set; }

    public bool IsAlive => _assemblyRef.IsAlive;

    /// <summary>
    /// Loads the plugin at <paramref name="path"/> into its own load context.
    /// Returns null if there is no assembly there.
    /// </summary>
    /// <remarks>
    /// Read into memory and loaded from bytes rather than <c>Assembly.LoadFrom</c>:
    /// a load-from context resolves the assembly's dependencies relative to the file
    /// it was loaded from, which is the plugin folder - the one place the shared
    /// assemblies deliberately are not.
    ///
    /// A non-collectible context cannot be given a second assembly of one identity, and
    /// cannot release the first, so there the load happens once per id and is reused.
    /// </remarks>
    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    public static PluginAssembly? Load(string id, string path)
    {
        if (!VFS.FileExists(path))
            return null;

        var cacheable = !PluginLoadContext.IsCollectibleSupported;

        if (!cacheable || !Resident.TryGetValue(id, out var loaded))
        {
            var loadContext = new PluginLoadContext($"Plugin:{id}");
            using var stream = VFS.OpenRead(path);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            loaded = (loadContext, loadContext.LoadFromStream(buffer));

            if (cacheable) Resident[id] = loaded;
        }

        return new PluginAssembly(id, path, loaded.Assembly, loaded.Context);
    }

    /// <summary>
    /// Finds the constructor of the single public, non-abstract subtype of
    /// <typeparamref name="T"/> the assembly declares.
    /// </summary>
    /// <remarks>
    /// A plugin declares exactly one implementation per contract, so a second is a
    /// packaging mistake rather than something to guess between.
    /// </remarks>
    [RequiresUnreferencedCode("Reflects over the assembly to find a constructor.")]
    public ConstructorInfo FindConstructor<T>(Type[] parameters)
        => Find<T>(parameters)
           ?? throw new InvalidOperationException(
               $"Plugin assembly '{Assembly.GetName().Name}' must declare exactly one {typeof(T).Name} subtype.");

    /// <summary>
    /// As <see cref="FindConstructor{T}"/>, but for a contract the plugin may legitimately
    /// not implement: returns null when it declares none.
    /// </summary>
    [RequiresUnreferencedCode("Reflects over the assembly to find a constructor.")]
    public ConstructorInfo? TryFindConstructor<T>(Type[] parameters) => Find<T>(parameters);

    [RequiresUnreferencedCode("Reflects over the assembly to find a constructor.")]
    private ConstructorInfo? Find<T>(Type[] parameters)
    {
        var types = Assembly.GetExportedTypes()
            .Where(t => !t.IsAbstract && typeof(T).IsAssignableFrom(t))
            .ToArray();

        if (types.Length == 0)
            return null;

        if (types.Length > 1)
        {
            throw new InvalidOperationException(
                $"Plugin assembly '{Assembly.GetName().Name}' must declare at most one {typeof(T).Name} " +
                $"subtype, but declares {types.Length}.");
        }

        return types[0].GetConstructor(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, parameters, null)
               ?? throw new InvalidOperationException(
                   $"Plugin '{Id}': {types[0].FullName} does not have the expected constructor.");
    }

    /// <summary>
    /// Detaches this plugin and asks its load context to unload. Returns immediately;
    /// the assembly is only collected once nothing references it any more. Does nothing
    /// where contexts cannot be collected.
    /// </summary>
    /// <remarks>
    /// Plugins declare their own MemoryPack event types, and the generated formatters
    /// register themselves into MemoryPack's static table from the plugin's module
    /// initializer - so the table holds them from the moment the plugin loads. It lives
    /// in the host's MemoryPack assembly, so it holds the plugin's types, and through
    /// them the whole context, and would keep the plugin resident for the lifetime of
    /// the process no matter what this method does. Dropping this plugin's entries is
    /// what actually makes the unload possible, and it has to happen while the types are
    /// still reachable through the reflection handles this object holds.
    /// </remarks>
    public void Unload()
    {
        if (_unloaded)
            return;

        _unloaded = true;

        // Without a collectible context nothing is released, and the cached assembly is what
        // the next run loads again - so its formatters and type handles have to stay too.
        if (!PluginLoadContext.IsCollectibleSupported)
            return;

        var dropped = MemoryPackFormatterRegistry.DropFormatters(Assembly);

        Assembly = null!;

        _loadContext.Unload();

        Logging.Info($"Unloading plugin '{Id}': {dropped} memorypack formatter(s) dropped");
    }
}
