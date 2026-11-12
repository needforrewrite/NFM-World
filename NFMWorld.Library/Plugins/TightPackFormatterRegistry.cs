using System.Reflection;
using TightPack;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// Lets go of formatter registrations belonging to a plugin assembly, so its collectible load
/// context can actually be collected.
/// </summary>
/// <remarks>
/// A <c>[TightPackable]</c> type's generated formatter registers itself into
/// <see cref="TightPackFormatterProvider"/> from the type's static constructor. That table is a
/// static on the host's TightPack assembly, so an entry holds the plugin's type - and through it
/// the plugin's whole load context - and keeps the assembly resident for the lifetime of the
/// process. Measured with MemoryPack, which had the same shape: a plugin declaring four event
/// types held four entries and its context unloaded only once they were dropped.
/// </remarks>
internal static class TightPackFormatterRegistry
{
    /// <summary>
    /// Drops every formatter whose key involves a type declared by <paramref name="assembly"/>.
    /// Returns how many were dropped.
    /// </summary>
    public static int DropFormatters(Assembly assembly) =>
        TightPackFormatterProvider.UnregisterAssembly(assembly);
}
