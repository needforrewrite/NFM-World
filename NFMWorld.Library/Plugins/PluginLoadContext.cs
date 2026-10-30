using System.Reflection;
using System.Runtime.Loader;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// The load context a plugin assembly is loaded into, so it can be unloaded again once
/// nothing is using it.
/// </summary>
/// <remarks>
/// Resolution deliberately falls through to the default context (by returning null)
/// for everything the plugin does not itself ship. Plugins are built without copies
/// of the host's assemblies - see <see cref="PluginAssembly"/> - so the shared
/// assemblies resolve against the host's copies rather than a second set. That is
/// also what makes them collectible: nothing in the plugin context is shared with
/// anything else.
///
/// Mono's wasm runtime has <see cref="AssemblyLoadContext"/> but no collectible
/// contexts, so there the plugin stays resident and one context per plugin is
/// reused for the life of the page. See <see cref="PluginAssembly.Load"/>.
/// </remarks>
internal sealed class PluginLoadContext(string name)
    : AssemblyLoadContext(name, isCollectible: IsCollectibleSupported)
{
    internal static bool IsCollectibleSupported => !OperatingSystem.IsBrowser();

    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
