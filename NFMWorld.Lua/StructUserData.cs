using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lua;
using Lua.Runtime;

namespace nfm_world_library.Lua;

/// <summary>
/// A generic struct-based userdata wrapper that implements <see cref="ILuaUserData"/> for any type <typeparamref name="T"/>.
/// Provides a shared metatable per <typeparamref name="T"/> with reflection-based field/property access,
/// array/list indexing, delegate invocation, and enumeration support.
/// </summary>
/// <typeparam name="T">The wrapped type (class, struct, interface, array, delegate, or BCL type).</typeparam>
/// <remarks>
/// The name "StructUserData" refers to the wrapper itself being a struct, not that <typeparamref name="T"/> must be a value type.
/// </remarks>
public struct StructUserData<T> : ILuaUserData
{
    /// <summary>
    /// The wrapped value.
    /// </summary>
    public T Value { get; init; }

    /// <summary>
    /// Optional callback invoked after the wrapped value is mutated.
    /// Used for InlineArray write propagation back to the parent object.
    /// </summary>
    internal Action<T>? OnMutated { get; init; }

    // ------------------------------------------------------------------
    // ILuaUserData
    // ------------------------------------------------------------------

    /// <inheritdoc/>
    public LuaTable? Metatable
    {
        readonly get => SharedMetatable;
        set => throw new InvalidOperationException(
            "Cannot override the shared metatable of StructUserData<T>.");
    }

    // ------------------------------------------------------------------
    // Shared metatable (one per T, thread-safe lazy init)
    // ------------------------------------------------------------------

    private static LuaTable? s_sharedMetatable;

    private static LuaTable SharedMetatable
    {
        get
        {
            if (s_sharedMetatable != null)
                return s_sharedMetatable;

            var mt = BuildMetatable();
            Interlocked.CompareExchange(ref s_sharedMetatable, mt, null);
            return s_sharedMetatable!;
        }
    }

    private static LuaTable BuildMetatable()
    {
        var mt = new LuaTable(0, 8);

        // __index: field/property/array access
        mt[Metamethods.Index] = new LuaFunction("__index", IndexMetamethodImpl);

        // __newindex: field/property/array write
        mt[Metamethods.NewIndex] = new LuaFunction("__newindex", NewIndexMetamethodImpl);

        // __len: for arrays, lists, collections with known length
        if (HasLengthSupport)
        {
            mt[Metamethods.Len] = new LuaFunction("__len", LenMetamethodImpl);
        }

        // __tostring
        mt[Metamethods.ToString] = new LuaFunction("__tostring", ToStringMetamethodImpl);

        // __pairs: for enumerable types
        if (typeof(IEnumerable).IsAssignableFrom(typeof(T)) || IsArray)
        {
            mt[Metamethods.Pairs] = new LuaFunction("__pairs", PairsMetamethodImpl);
        }

        // __call: for delegate types
        if (typeof(Delegate).IsAssignableFrom(typeof(T)))
        {
            mt[Metamethods.Call] = new LuaFunction("__call", CallMetamethodImpl);
        }

        // Register instance methods as entries on the metatable
        RegisterMethods(mt);

        return mt;
    }

    // ------------------------------------------------------------------
    // Type capability checks
    // ------------------------------------------------------------------

    private static bool IsArray => typeof(T).IsArray;

    private static bool HasLengthSupport
    {
        get
        {
            var t = typeof(T);
            if (t.IsArray) return true;
            if (t.IsAssignableTo(typeof(ICollection))) return true;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IList<>)) return true;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)) return true;
            // InlineArray: detected by [InlineArray] attribute
            if (t.GetCustomAttribute<InlineArrayAttribute>() != null) return true;
            return false;
        }
    }

    private static int GetLength(T value)
    {
        return value switch
        {
            Array arr => arr.Length,
            ICollection col => col.Count,
            _ => GetLengthViaProperty(value),
        };
    }

    private static int GetLengthViaProperty(T value)
    {
        var prop = typeof(T).GetProperty("Count")
            ?? typeof(T).GetProperty("Length");
        if (prop != null && prop.CanRead)
        {
            var result = prop.GetValue(value);
            if (result is int intVal) return intVal;
            return Convert.ToInt32(result);
        }
        return 0;
    }

    // ------------------------------------------------------------------
    // __index metamethod
    // ------------------------------------------------------------------

    private static ValueTask<int> IndexMetamethodImpl(
        LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var wrapper = context.GetArgument<StructUserData<T>>(0);
        var key = context.GetArgument(1);
        var value = wrapper.Value;

        // Integer key → array/list index (1-based Lua → 0-based C#)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var csharpIndex))
        {
            if (TryGetByIndex(value, csharpIndex, out var element))
            {
                return new(context.Return(LuaValue.FromObject(element!)));
            }
            return new(context.Return(LuaValue.Nil));
        }

        // String key → field/property/method lookup
        if (key.TryRead<string>(out var stringKey))
        {
            // Try properties (case-insensitive)
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (string.Equals(prop.Name, stringKey, StringComparison.OrdinalIgnoreCase)
                    && prop.CanRead)
                {
                    var result = prop.GetValue(value);
                    return new(context.Return(LuaValue.FromObject(result!)));
                }
            }

            // Try fields (case-insensitive)
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, stringKey, StringComparison.OrdinalIgnoreCase))
                {
                    var result = field.GetValue(value);
                    return new(context.Return(LuaValue.FromObject(result!)));
                }
            }

            // Try methods registered on the metatable
            var mt = SharedMetatable;
            var methodKey = new LuaValue(stringKey);
            var methodEntry = mt[methodKey];
            if (methodEntry.Type == LuaValueType.Function)
            {
                return new(context.Return(methodEntry));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    // ------------------------------------------------------------------
    // __newindex metamethod
    // ------------------------------------------------------------------

    private static ValueTask<int> NewIndexMetamethodImpl(
        LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var wrapper = context.GetArgument<StructUserData<T>>(0);
        var key = context.GetArgument(1);
        var newValue = context.GetArgument(2);
        var value = wrapper.Value;
        var mutated = false;

        // Integer key → array/list index write
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var csharpIndex))
        {
            if (TrySetByIndex(ref value, csharpIndex, newValue))
            {
                mutated = true;
                goto done;
            }
            throw new LuaRuntimeException(context.State,
                $"Cannot set index {csharpIndex} on {typeof(T).Name}");
        }

        // String key → field/property write
        if (key.TryRead<string>(out var stringKey))
        {
            // Try properties (case-insensitive)
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (string.Equals(prop.Name, stringKey, StringComparison.OrdinalIgnoreCase)
                    && prop.CanWrite)
                {
                    var converted = ConvertLuaValue(newValue, prop.PropertyType);
                    prop.SetValue(value, converted);
                    mutated = true;
                    goto done;
                }
            }

            // Try fields (case-insensitive)
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, stringKey, StringComparison.OrdinalIgnoreCase)
                    && !field.IsInitOnly)
                {
                    var converted = ConvertLuaValue(newValue, field.FieldType);
                    field.SetValue(value, converted);
                    mutated = true;
                    goto done;
                }
            }

            throw new LuaRuntimeException(context.State,
                $"'{stringKey}' not found or is read-only on {typeof(T).Name}");
        }

        done:
        if (mutated && wrapper.OnMutated != null)
        {
            wrapper.OnMutated(value);
        }

        return new(context.Return());
    }

    // ------------------------------------------------------------------
    // __len metamethod
    // ------------------------------------------------------------------

    private static ValueTask<int> LenMetamethodImpl(
        LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var wrapper = context.GetArgument<StructUserData<T>>(0);
        var len = GetLength(wrapper.Value);
        return new(context.Return((double)len));
    }

    // ------------------------------------------------------------------
    // __tostring metamethod
    // ------------------------------------------------------------------

    private static ValueTask<int> ToStringMetamethodImpl(
        LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var wrapper = context.GetArgument<StructUserData<T>>(0);
        var str = $"StructUserData<{typeof(T).Name}>: {wrapper.Value}";
        return new(context.Return(str));
    }

    // ------------------------------------------------------------------
    // __pairs metamethod
    // ------------------------------------------------------------------

    private static ValueTask<int> PairsMetamethodImpl(
        LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var wrapper = context.GetArgument<StructUserData<T>>(0);
        var value = wrapper.Value;
        var enumerable = value as IEnumerable;
        if (enumerable == null)
        {
            return new(context.Return(LuaValue.Nil, LuaValue.Nil, LuaValue.Nil));
        }

        var enumerator = enumerable.GetEnumerator();
        var iteratorFunc = new LuaFunction("__pairs_iterator", (ctx, cts) =>
        {
            if (!enumerator.MoveNext())
            {
                return new(ctx.Return(LuaValue.Nil));
            }
            var current = LuaValue.FromObject(enumerator.Current!);
            return new(ctx.Return(current));
        });

        return new(context.Return(iteratorFunc, LuaValue.Nil, LuaValue.Nil));
    }

    // ------------------------------------------------------------------
    // __call metamethod (for delegates)
    // ------------------------------------------------------------------

    private static ValueTask<int> CallMetamethodImpl(
        LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var wrapper = context.GetArgument<StructUserData<T>>(0);
        if (wrapper.Value is not Delegate del)
        {
            throw new LuaRuntimeException(context.State,
                $"{typeof(T).Name} is not callable");
        }

        // Build argument array from Lua stack
        var method = del.GetMethodInfo();
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length && i < context.ArgumentCount - 1; i++)
        {
            args[i] = ConvertLuaValue(context.GetArgument(i + 1), parameters[i].ParameterType);
        }

        var result = del.DynamicInvoke(args);
        return new(context.Return(LuaValue.FromObject(result!)));
    }

    // ------------------------------------------------------------------
    // Method registration on metatable
    // ------------------------------------------------------------------

    private static void RegisterMethods(LuaTable mt)
    {
        var methods = typeof(T).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods)
        {
            // Skip special-name methods (operators, property accessors, events)
            if (method.IsSpecialName) continue;
            // Skip methods inherited from System.Object (except ToString)
            if (method.DeclaringType == typeof(object) && method.Name != "ToString") continue;

            var capturedMethod = method;
            var methodName = char.ToLowerInvariant(method.Name[0]) + method.Name[1..];

            mt[methodName] = new LuaFunction(methodName, (context, cts) =>
            {
                var wrapper = context.GetArgument<StructUserData<T>>(0);
                var parameters = capturedMethod.GetParameters();
                var args = new object?[parameters.Length];

                for (var i = 0; i < parameters.Length && i < context.ArgumentCount - 1; i++)
                {
                    args[i] = ConvertLuaValue(
                        context.GetArgument(i + 1),
                        parameters[i].ParameterType);
                }

                object? result;
                if (capturedMethod.IsStatic)
                {
                    result = capturedMethod.Invoke(null, args);
                }
                else
                {
                    result = capturedMethod.Invoke(wrapper.Value, args);
                }

                if (capturedMethod.ReturnType == typeof(void))
                {
                    return new(context.Return());
                }

                return new(context.Return(LuaValue.FromObject(result!)));
            });
        }
    }

    // ------------------------------------------------------------------
    // Array/InlineArray index helpers
    // ------------------------------------------------------------------

    private static bool TryGetByIndex(T value, int index, out object? result)
    {
        result = null;

        if (value is Array arr)
        {
            if ((uint)index < (uint)arr.Length)
            {
                result = arr.GetValue(index);
                return true;
            }
            return false;
        }

        if (value is IList list)
        {
            if ((uint)index < (uint)list.Count)
            {
                result = list[index];
                return true;
            }
            return false;
        }

        // InlineArray: use Unsafe.Add
        var inlineArrayAttr = typeof(T).GetCustomAttribute<InlineArrayAttribute>();
        if (inlineArrayAttr != null)
        {
            var length = inlineArrayAttr.Length;
            if ((uint)index < (uint)length)
            {
                ref var firstElement = ref Unsafe.As<T, byte>(ref value);
                var elementSize = Unsafe.SizeOf<T>() / length;
                ref var element = ref Unsafe.Add(ref firstElement, index * elementSize);
                result = RuntimeHelpers.GetObjectValue(
                    Unsafe.As<byte, object>(ref element));
                return true;
            }
            return false;
        }

        // Indexer property (this[int])
        var indexer = typeof(T).GetProperty("Item",
            BindingFlags.Public | BindingFlags.Instance,
            null, typeof(T), [typeof(int)], null);
        if (indexer != null && indexer.CanRead)
        {
            result = indexer.GetValue(value, [index]);
            return true;
        }

        return false;
    }

    private static bool TrySetByIndex(ref T value, int index, LuaValue newValue)
    {
        if (value is Array arr)
        {
            if ((uint)index < (uint)arr.Length)
            {
                var converted = ConvertLuaValue(newValue, arr.GetType().GetElementType()!);
                arr.SetValue(converted, index);
                return true;
            }
            return false;
        }

        if (value is IList list)
        {
            if ((uint)index < (uint)list.Count)
            {
                list[index] = newValue.Read<object>();
                return true;
            }
            return false;
        }

        // InlineArray: use Unsafe.Add
        var inlineArrayAttr = typeof(T).GetCustomAttribute<InlineArrayAttribute>();
        if (inlineArrayAttr != null)
        {
            var length = inlineArrayAttr.Length;
            if ((uint)index < (uint)length)
            {
                ref var firstElement = ref Unsafe.As<T, byte>(ref value);
                var elementSize = Unsafe.SizeOf<T>() / length;
                ref var element = ref Unsafe.Add(ref firstElement, index * elementSize);
                var elementType = typeof(T).GetElementType()
                    ?? typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public)[0].FieldType;
                var converted = ConvertLuaValue(newValue, elementType);
                Unsafe.As<byte, object>(ref element) = converted!;
                return true;
            }
            return false;
        }

        // Indexer property setter
        var indexer = typeof(T).GetProperty("Item",
            BindingFlags.Public | BindingFlags.Instance,
            null, typeof(T), [typeof(int)], null);
        if (indexer != null && indexer.CanWrite)
        {
            var converted = ConvertLuaValue(newValue, indexer.PropertyType);
            indexer.SetValue(value, converted, [index]);
            return true;
        }

        return false;
    }

    // ------------------------------------------------------------------
    // Lua value conversion helpers
    // ------------------------------------------------------------------

    private static object? ConvertLuaValue(LuaValue luaValue, Type targetType)
    {
        if (luaValue.Type == LuaValueType.Nil)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        if (luaValue.TryRead<ILuaUserData>(out var userData) && userData is StructUserData<T> wrapper)
            return wrapper.Value;

        return luaValue.Read<object>();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Checks whether a Lua number represents a valid 1-based array index,
    /// and converts it to a 0-based C# index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLuaIndex(double num, out int csharpIndex)
    {
        if (double.IsFinite(num) && num >= 1.0
            && num == Math.Floor(num) && num <= int.MaxValue)
        {
            csharpIndex = (int)num - 1;
            return true;
        }
        csharpIndex = 0;
        return false;
    }

    // ------------------------------------------------------------------
    // Implicit conversion to LuaValue
    // ------------------------------------------------------------------

    public static implicit operator LuaValue(StructUserData<T> value)
        => LuaValue.FromUserData(value);
}
