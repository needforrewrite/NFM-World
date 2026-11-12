using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TightPack;

/// <summary>
/// Formatter registration for types that are resolved at runtime rather than at compile time.
/// Generated code holds its own formatter references, so this table is only on the path of types
/// that could not be named when the code was generated.
/// </summary>
/// <remarks>
/// Lookups are memoised per instantiation in <see cref="Cache{T}"/>. That does not pin a plugin:
/// the runtime allocates a generic instantiation over a collectible load context's types in that
/// context's own loader allocator, so a memo for a plugin type is collected with it. The
/// non-collectible surface is <c>s_formatters</c>, which <see cref="UnregisterAssembly"/> clears.
/// Registrations bump <c>s_generation</c>, which invalidates every memo at once.
/// </remarks>
public static class TightPackFormatterProvider
{
    private static readonly ConcurrentDictionary<Type, object> s_formatters = new();

    /// <summary>Bumped by every registration change, so memoised lookups resolve again.</summary>
    private static int s_generation;

    /// <summary>Memoised <see cref="TryGetFormatter{T}"/> result for one closed type.</summary>
    private static class Cache<T>
    {
        public static TightPackFormatter<T>? Formatter;
        public static int Generation;
    }

    public static void Register<T>(TightPackFormatter<T> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        s_formatters[typeof(T)] = formatter;
        Interlocked.Increment(ref s_generation);
    }

    /// <summary>Registers the formatter of a <c>[TightPackable]</c> type without naming it.</summary>
    public static void RegisterPackable<T>()
        where T : ITightPackable<T> =>
        Register(new TightPackableFormatter<T>());

    /// <summary>
    /// Whether a formatter for <paramref name="type"/> has been registered. A generated wrapper
    /// registers its own arguments, so this reports whether that happened rather than whether one
    /// could be built on demand.
    /// </summary>
    public static bool IsRegistered(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return s_formatters.ContainsKey(type);
    }

    /// <summary>Drops the formatter registered for exactly <paramref name="type"/>.</summary>
    public static bool Unregister(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!s_formatters.TryRemove(type, out _))
        {
            return false;
        }

        Interlocked.Increment(ref s_generation);
        return true;
    }

    /// <summary>
    /// Drops every registration whose key involves a type declared by <paramref name="assembly"/>,
    /// so a collectible plugin load context is not pinned by this table. Constructed types count:
    /// <c>List&lt;PluginEvent&gt;</c> lives in the core library but is only ever used for one plugin.
    /// </summary>
    public static int UnregisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        int dropped = 0;
        foreach (KeyValuePair<Type, object> entry in s_formatters)
        {
            if (Involves(entry.Key, assembly) && s_formatters.TryRemove(entry.Key, out _))
            {
                dropped++;
            }
        }

        if (dropped > 0)
        {
            Interlocked.Increment(ref s_generation);
        }

        return dropped;
    }

    private static bool Involves(Type type, Assembly assembly)
    {
        if (type.Assembly == assembly)
        {
            return true;
        }

        if (type.HasElementType)
        {
            return Involves(type.GetElementType()!, assembly);
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            if (Involves(argument, assembly))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetFormatter<T>([NotNullWhen(true)] out TightPackFormatter<T>? formatter)
    {
        // Built-ins are plain classes rather than generic instantiations, so they must reach the
        // dictionary before it is consulted. Done first: it registers ~60 formatters, and doing it
        // inside the resolve would move the generation for every lookup in a fresh process.
        Internal.DefaultFormatters.EnsureBuiltinsRegistered();

        // Read as a pair: the formatter is only trusted when it was memoised under the generation
        // currently in force, so a registration that landed after it was written is never hidden.
        int generation = Volatile.Read(ref s_generation);

        if (Volatile.Read(ref Cache<T>.Generation) == generation
            && Volatile.Read(ref Cache<T>.Formatter) is { } cached)
        {
            formatter = cached;
            return true;
        }

        if (!Resolve<T>(out formatter))
        {
            return false;
        }

        // A registration during the resolve may have changed the answer, so this lookup is served
        // without being memoised rather than memoising a stale result.
        if (Volatile.Read(ref s_generation) == generation)
        {
            Volatile.Write(ref Cache<T>.Formatter, formatter);
            Volatile.Write(ref Cache<T>.Generation, generation);
        }

        return true;
    }

    private static bool Resolve<T>([NotNullWhen(true)] out TightPackFormatter<T>? formatter)
    {
        if (s_formatters.TryGetValue(typeof(T), out object? boxed))
        {
            formatter = (TightPackFormatter<T>)boxed;
            return true;
        }

        if (Internal.DefaultFormatters.TryCreate(typeof(T)) is TightPackFormatter<T> created)
        {
            formatter = created;
            return true;
        }

        formatter = null;
        return false;
    }

    public static TightPackFormatter<T> GetFormatter<T>()
    {
        if (TryGetFormatter<T>(out var formatter))
        {
            return formatter;
        }

        TightPackSerializationException.ThrowNotRegistered(typeof(T));
        return null!;
    }

    /// <summary>
    /// Untyped lookup, for a caller that only has a <see cref="Type"/> - a plugin's event type
    /// reached by reflection, for instance. Every other path is generic and never comes here.
    /// </summary>
    public static object? GetFormatter(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        Internal.DefaultFormatters.EnsureBuiltinsRegistered();

        if (s_formatters.TryGetValue(type, out object? registered))
        {
            return registered;
        }

        return Internal.DefaultFormatters.TryCreate(type);
    }
}
