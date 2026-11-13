using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace TightPack.Internal;

/// <summary>
/// Builds a formatter for a type nobody registered explicitly. Runs once per type, on the first
/// lookup that misses, and its result is cached by <see cref="TightPackFormatterProvider"/>, so the
/// reflection never appears on a hot path.
/// </summary>
internal static class DefaultFormatters
{
    private const int BuiltinsPending = 0;
    private const int BuiltinsRegistering = 1;
    private const int BuiltinsDone = 2;

    private static int s_builtinsState;

    /// <summary>
    /// Runtime-resolved generic types, mapped to the formatter that handles each. One table because
    /// two places must agree: this file builds them, and the generator classifies and names the same
    /// types for the code it emits.
    /// </summary>
    private static readonly Dictionary<Type, Type> GenericFormatters = new()
    {
        [typeof(Stack<>)] = typeof(Formatters.StackFormatter<>),
        [typeof(Queue<>)] = typeof(Formatters.QueueFormatter<>),
        [typeof(LinkedList<>)] = typeof(Formatters.LinkedListFormatter<>),
        [typeof(SortedSet<>)] = typeof(Formatters.SortedSetFormatter<>),
        [typeof(Collection<>)] = typeof(Formatters.CollectionFormatter<>),
        [typeof(ObservableCollection<>)] = typeof(Formatters.ObservableCollectionFormatter<>),
        [typeof(ConcurrentQueue<>)] = typeof(Formatters.ConcurrentQueueFormatter<>),
        [typeof(ConcurrentStack<>)] = typeof(Formatters.ConcurrentStackFormatter<>),
        [typeof(ConcurrentBag<>)] = typeof(Formatters.ConcurrentBagFormatter<>),
        [typeof(ReadOnlyCollection<>)] = typeof(Formatters.ReadOnlyCollectionFormatter<>),
        [typeof(ReadOnlyObservableCollection<>)] = typeof(Formatters.ReadOnlyObservableCollectionFormatter<>),
        [typeof(BlockingCollection<>)] = typeof(Formatters.BlockingCollectionFormatter<>),
        [typeof(ArraySegment<>)] = typeof(Formatters.ArraySegmentFormatter<>),
        [typeof(ISet<>)] = typeof(Formatters.InterfaceSetFormatter<>),
        [typeof(IReadOnlySet<>)] = typeof(Formatters.IReadOnlySetFormatter<>),
        [typeof(PriorityQueue<,>)] = typeof(Formatters.PriorityQueueFormatter<,>),
        [typeof(SortedDictionary<,>)] = typeof(Formatters.SortedDictionaryFormatter<,>),
        [typeof(SortedList<,>)] = typeof(Formatters.SortedListFormatter<,>),
        [typeof(ConcurrentDictionary<,>)] = typeof(Formatters.ConcurrentDictionaryFormatter<,>),
    };

    /// <summary>Multi-dimensional arrays need one formatter per rank; the shape is part of the type.</summary>
    private static readonly Dictionary<int, Type> ArrayFormattersByRank = new()
    {
        [2] = typeof(Formatters.TwoDimensionalArrayFormatter<>),
        [3] = typeof(Formatters.ThreeDimensionalArrayFormatter<>),
        [4] = typeof(Formatters.FourDimensionalArrayFormatter<>),
    };

    public static object? TryCreate(Type type)
    {
        EnsureBuiltinsRegistered();

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Create(typeof(Formatters.NullableFormatter<>), underlying);
        }

        if (type.IsEnum)
        {
            return Create(typeof(Formatters.EnumFormatter<>), type);
        }

        if (type.IsArray)
        {
            if (type.GetArrayRank() == 1)
            {
                return Create(typeof(Formatters.ArrayFormatter<>), type.GetElementType()!);
            }

            return ArrayFormattersByRank.TryGetValue(type.GetArrayRank(), out Type? ranked)
                ? Create(ranked, type.GetElementType()!)
                : null;
        }

        if (type == typeof(Type))
        {
            return Formatters.TypeFormatter.Default;
        }

        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            Type[] arguments = type.GetGenericArguments();

            if (GenericFormatters.TryGetValue(definition, out Type? generic))
            {
                return Create(generic, arguments);
            }

            if (definition == typeof(List<>))
            {
                return Create(typeof(Formatters.ListFormatter<>), arguments);
            }

            if (definition == typeof(Dictionary<,>))
            {
                return Create(typeof(Formatters.DictionaryFormatter<,>), arguments);
            }

            if (definition == typeof(HashSet<>))
            {
                return Create(typeof(Formatters.HashSetFormatter<>), arguments);
            }

            if (definition == typeof(KeyValuePair<,>))
            {
                return Create(typeof(Formatters.KeyValuePairFormatter<,>), arguments);
            }

            if (definition == typeof(IEnumerable<>))
            {
                return Create(typeof(Formatters.EnumerableFormatter<>), arguments);
            }

            if (definition == typeof(IList<>))
            {
                return Create(typeof(Formatters.InterfaceListFormatter<>), arguments);
            }

            if (definition == typeof(IReadOnlyList<>))
            {
                return Create(typeof(Formatters.IReadOnlyListFormatter<>), arguments);
            }

            if (definition == typeof(ICollection<>))
            {
                return Create(typeof(Formatters.InterfaceCollectionFormatter<>), arguments);
            }

            if (definition == typeof(IReadOnlyCollection<>))
            {
                return Create(typeof(Formatters.IReadOnlyCollectionFormatter<>), arguments);
            }

            if (definition == typeof(IDictionary<,>))
            {
                return Create(typeof(Formatters.InterfaceDictionaryFormatter<,>), arguments);
            }

            if (definition == typeof(IReadOnlyDictionary<,>))
            {
                return Create(typeof(Formatters.IReadOnlyDictionaryFormatter<,>), arguments);
            }

            if (definition == typeof(ImmutableArray<>))
            {
                return Create(typeof(Formatters.ImmutableArrayFormatter<>), arguments);
            }

            if (definition == typeof(ReadOnlyMemory<>))
            {
                return arguments[0] == typeof(byte)
                    ? new Formatters.ReadOnlyMemoryByteFormatter()
                    : Create(typeof(Formatters.ReadOnlyMemoryFormatter<>), arguments);
            }

            if (definition == typeof(Memory<>))
            {
                return arguments[0] == typeof(byte)
                    ? new Formatters.MemoryByteFormatter()
                    : Create(typeof(Formatters.MemoryFormatter<>), arguments);
            }

            if (definition == typeof(InlineArray2<>))
            {
                return Create(typeof(Formatters.InlineArray2Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray3<>))
            {
                return Create(typeof(Formatters.InlineArray3Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray4<>))
            {
                return Create(typeof(Formatters.InlineArray4Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray5<>))
            {
                return Create(typeof(Formatters.InlineArray5Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray6<>))
            {
                return Create(typeof(Formatters.InlineArray6Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray7<>))
            {
                return Create(typeof(Formatters.InlineArray7Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray8<>))
            {
                return Create(typeof(Formatters.InlineArray8Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray9<>))
            {
                return Create(typeof(Formatters.InlineArray9Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray10<>))
            {
                return Create(typeof(Formatters.InlineArray10Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray11<>))
            {
                return Create(typeof(Formatters.InlineArray11Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray12<>))
            {
                return Create(typeof(Formatters.InlineArray12Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray13<>))
            {
                return Create(typeof(Formatters.InlineArray13Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray14<>))
            {
                return Create(typeof(Formatters.InlineArray14Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray15<>))
            {
                return Create(typeof(Formatters.InlineArray15Formatter<>), arguments);
            }

            if (definition == typeof(InlineArray16<>))
            {
                return Create(typeof(Formatters.InlineArray16Formatter<>), arguments);
            }

            Type? tuple = definition switch
            {
                _ when definition == typeof(ValueTuple<>) => typeof(Formatters.ValueTupleFormatter<>),
                _ when definition == typeof(ValueTuple<,>) => typeof(Formatters.ValueTupleFormatter<,>),
                _ when definition == typeof(ValueTuple<,,>) => typeof(Formatters.ValueTupleFormatter<,,>),
                _ when definition == typeof(ValueTuple<,,,>) => typeof(Formatters.ValueTupleFormatter<,,,>),
                _ when definition == typeof(ValueTuple<,,,,>) => typeof(Formatters.ValueTupleFormatter<,,,,>),
                _ when definition == typeof(ValueTuple<,,,,,>) => typeof(Formatters.ValueTupleFormatter<,,,,,>),
                _ when definition == typeof(ValueTuple<,,,,,,>) => typeof(Formatters.ValueTupleFormatter<,,,,,,>),
                _ when definition == typeof(ValueTuple<,,,,,,,>) => typeof(Formatters.ValueTupleFormatter<,,,,,,,>),
                _ => null,
            };

            if (tuple is not null)
            {
                return Create(tuple, arguments);
            }
        }

        // Safety net: a generated type works even if its registration never ran.
        if (Array.Exists(type.GetInterfaces(), i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(ITightPackable<>)
                && i.GetGenericArguments()[0] == type
        ))
        {
            return Create(typeof(TightPackableFormatter<>), type);
        }

        return null;
    }

    internal static void EnsureBuiltinsRegistered()
    {
        // A plain read on every lookup; the interlocked path is only paid on the first one.
        if (Volatile.Read(ref s_builtinsState) == BuiltinsDone)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref s_builtinsState, BuiltinsRegistering, BuiltinsPending) != BuiltinsPending)
        {
            // Another thread is registering; wait for it rather than serve a half-filled table.
            SpinWait spin = default;
            while (Volatile.Read(ref s_builtinsState) != BuiltinsDone)
            {
                spin.SpinOnce();
            }

            return;
        }

        Formatters.BuiltinFormatters.RegisterAll();
        Volatile.Write(ref s_builtinsState, BuiltinsDone);
    }

    private static object? Create(Type openGeneric, params Type[] arguments)
    {
        return Activator.CreateInstance(openGeneric.MakeGenericType(arguments));
    }
}
