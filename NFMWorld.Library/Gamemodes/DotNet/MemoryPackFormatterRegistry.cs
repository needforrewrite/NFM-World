using System.Collections.Concurrent;
using System.Reflection;
using MemoryPack;

namespace NFMWorldLibrary.Gamemodes.DotNet;

/// <summary>
/// Lets go of MemoryPack formatter registrations belonging to a plugin assembly, so
/// its collectible load context can actually be collected.
/// </summary>
/// <remarks>
/// A <c>[MemoryPackable]</c> type's generated formatter registers itself into
/// <see cref="MemoryPackFormatterProvider"/> the first time the type is serialized.
/// That table is a static on the host's MemoryPack assembly, so an entry holds the
/// plugin's type - and through it the plugin's whole load context - and keeps the
/// assembly resident for the lifetime of the process. Measured: a plugin declaring four
/// event types registers four entries, and the context unloads only once they are
/// dropped.
///
/// Reached by reflection because the table is private and MemoryPack offers no way to
/// withdraw a registration. That is a deliberate trade: the alternative is passing
/// <see cref="MemoryPackSerializerOptions"/> with a custom formatter provider through
/// every gamemode event, which would have to be threaded through the serializer calls
/// on both sides of the wire.
/// </remarks>
internal static class MemoryPackFormatterRegistry
{
    private static readonly ConcurrentDictionary<Type, IMemoryPackFormatter> Formatters =
        (ConcurrentDictionary<Type, IMemoryPackFormatter>)typeof(MemoryPackFormatterProvider)
            .GetField("formatters", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    /// <summary>
    /// Drops every formatter whose key involves a type declared by
    /// <paramref name="assembly"/>. Returns how many were dropped.
    /// </summary>
    public static int DropFormatters(Assembly assembly)
    {
        var dropped = 0;

        foreach (var entry in Formatters)
        {
            if (Involves(entry.Key, assembly) && Formatters.TryRemove(entry.Key, out _))
                dropped++;
        }

        return dropped;
    }

    /// <summary>
    /// Whether <paramref name="type"/> is, or is constructed from, a type declared by
    /// <paramref name="assembly"/>. The constructed case matters because MemoryPack
    /// keys those entries by the constructed type (<c>List&lt;PluginEvent&gt;</c>),
    /// which itself lives in the core library and so would not match on its own.
    /// </summary>
    private static bool Involves(Type type, Assembly assembly)
    {
        if (type.Assembly == assembly)
            return true;

        if (type.HasElementType && Involves(type.GetElementType()!, assembly))
            return true;

        return type.IsGenericType
               && type.GetGenericArguments().Any(argument => Involves(argument, assembly));
    }
}
