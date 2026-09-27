using System.Reflection;
using System.Runtime.Loader;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// The load context a gamemode plugin assembly is loaded into, so it can be unloaded
/// again once no gamemode is using it.
/// </summary>
/// <remarks>
/// Resolution deliberately falls through to the default context (by returning null)
/// for everything the plugin does not itself ship. Plugins are built without copies
/// of the host's assemblies - see <see cref="GamemodePlugin"/> - so the shared
/// assemblies resolve against the host's copies rather than a second set. That is
/// also what makes them collectible: nothing in the plugin context is shared with
/// anything else.
/// </remarks>
internal sealed class PluginLoadContext(string name) : AssemblyLoadContext(name, isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
