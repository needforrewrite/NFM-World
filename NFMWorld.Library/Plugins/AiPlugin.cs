using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NFMWorldLibrary.Backend.AI;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// An AI loaded from a compiled plugin assembly (<c>data/ais/&lt;id&gt;/ai.dll</c>).
/// </summary>
/// <remarks>
/// Loading, reflection and unloading are <see cref="PluginAssembly"/>'s; this only adds
/// the AI contract's constructor.
/// </remarks>
public sealed class AiPlugin
{
    private static readonly Type[] ConstructorParameters =
        [typeof(ClientSidePlayer), typeof(string), typeof(BaseClientGamemode)];

    private readonly PluginAssembly _assembly;
    private readonly ConstructorInfo _constructor;

    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    private AiPlugin(string id, string name, PluginAssembly assembly, ConstructorInfo constructor)
    {
        Id = id;
        Name = name;
        _assembly = assembly;
        _constructor = constructor;
    }

    /// <summary>AI identifier, e.g. <c>el_stupido</c>.</summary>
    public string Id { get; }

    /// <summary>Display name, from <c>[assembly: Ai]</c>.</summary>
    public string Name { get; }

    public Assembly Assembly => _assembly.Assembly;

    public bool IsAlive => _assembly.IsAlive;

    /// <summary>
    /// Loads the AI at <paramref name="path"/> into its own load context, or returns null
    /// if there is no assembly there or it is not marked <c>[assembly: Ai]</c>.
    /// </summary>
    [RequiresUnreferencedCode("Loads an assembly into a collectible context.")]
    public static AiPlugin? Load(string id, string path)
    {
        var assembly = PluginAssembly.Load(id, path);
        if (assembly is null)
            return null;

        if (assembly.Assembly.GetCustomAttribute<AiAttribute>() is not { } attribute)
        {
            assembly.Unload();
            return null;
        }

        return new AiPlugin(id, attribute.Name, assembly, assembly.FindConstructor<PluginAi>(ConstructorParameters));
    }

    /// <summary>Creates an AI instance driving <paramref name="player"/>.</summary>
    public PluginAi Create(ClientSidePlayer player, BaseClientGamemode gamemode)
        => (PluginAi)_constructor.Invoke([player, Id, gamemode]);

    /// <summary>
    /// Detaches this plugin and asks its load context to unload. The instances it created
    /// must be dropped first, or the assembly stays resident.
    /// </summary>
    public void Unload()
    {
        var assembly = _assembly.Assembly;
        _assembly.Unload();

#if DEBUG
        // Same rule as the gamemode factory: without a collectible context nothing is
        // released, and the cached assembly is reused, so only check where unloading is
        // possible. An unload that silently fails is otherwise invisible.
        if (PluginLoadContext.IsCollectibleSupported)
            PluginUnloadVerifier.Arm(Id, new WeakReference(assembly));
#endif
    }
}
