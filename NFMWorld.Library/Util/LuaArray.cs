using System.Runtime.CompilerServices;
using Lua;
using Lua.Runtime;

namespace NFMWorldLibrary.Util;

/// <summary>
/// A lua-compatible T[]
/// </summary>
/// <param name="length">The length of the array</param>
/// <typeparam name="T">The type of the array's elements</typeparam>
public class LuaArray<T>(int length) : ILuaUserData
{
    public readonly T[] Value = new T[length];
    
    public T this[int index]
    {
        get => Value[index];
        set => Value[index] = value;
    }
    
    public static implicit operator Span<T>(LuaArray<T> array) => array.Value.AsSpan();
    public static implicit operator ReadOnlySpan<T>(LuaArray<T> array) => array.Value.AsSpan();

    // ------------------------------------------------------------------
    // ILuaUserData — table-like behaviour via metatable
    // ------------------------------------------------------------------

    LuaTable? ILuaUserData.Metatable
    {
        get
        {
            if (field == null)
            {
                field = SharedMetatable;
            }
            return field;
        }
        set;
    }

    /// <summary>Shared metatable for all <see cref="UnlimitedArray{T}"/> instances of the same T.</summary>
    private static LuaTable SharedMetatable
    {
        get
        {
            if (field != null)
                return field;

            var mt = new LuaTable(0, 3);
            mt[Metamethods.Index] = new LuaFunction("__index", IndexMetamethodImpl);
            mt[Metamethods.NewIndex] = new LuaFunction("__newindex", NewIndexMetamethodImpl);
            mt[Metamethods.Len] = new LuaFunction("__len", LenMetamethodImpl);

            Interlocked.CompareExchange(ref field, mt, null);
            return field!;
        }
    }

    private static ValueTask<int> IndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<UnlimitedArray<T>>(0);
        var key = context.GetArgument(1);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var index))
        {
            if ((uint)index < (uint)arr.Count)
            {
                return new(context.Return(LuaValue.FromObject(arr[index]!)));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    private static ValueTask<int> NewIndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<UnlimitedArray<T>>(0);
        var key = context.GetArgument(1);
        var value = context.GetArgument(2);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var index))
        {
            if (!value.TryRead<T>(out var typedValue))
            {
                // Fallback: try number → T conversion for common numeric types
                typedValue = ConvertLuaValue(value);
            }
            arr[index] = typedValue;
        }

        return new(context.Return());
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<UnlimitedArray<T>>(0);
        return new(context.Return((double)arr.Count));
    }

    /// <summary>
    /// Checks whether a Lua number represents a valid 1-based array index,
    /// and converts it to a 0-based C# index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLuaIndex(double num, out int csharpIndex)
    {
        // Must be a finite integer ≥ 1 (Lua arrays are 1-indexed)
        if (double.IsFinite(num) && num >= 1.0 && num == Math.Floor(num) && num <= int.MaxValue)
        {
            csharpIndex = (int)num - 1;
            return true;
        }

        csharpIndex = 0;
        return false;
    }

    /// <summary>Converts a <see cref="LuaValue"/> to <typeparamref name="T"/> with flexible coercion.</summary>
    private static T ConvertLuaValue(LuaValue value)
    {
        // Let LuaValue's own conversion handle it (supports double, string, bool, etc.)
        if (value.TryRead<T>(out var result))
            return result;

        // For numeric types, try reading as double and converting
        if (value.TryRead<double>(out var num))
        {
            var targetType = typeof(T);
            if (targetType == typeof(float))  { var v = (float)num;  return Unsafe.As<float, T>(ref v); }
            if (targetType == typeof(int))    { var v = (int)num;    return Unsafe.As<int, T>(ref v); }
            if (targetType == typeof(long))   { var v = (long)num;   return Unsafe.As<long, T>(ref v); }
            if (targetType == typeof(uint))   { var v = (uint)num;   return Unsafe.As<uint, T>(ref v); }
            if (targetType == typeof(ulong))  { var v = (ulong)num;  return Unsafe.As<ulong, T>(ref v); }
            if (targetType == typeof(short))  { var v = (short)num;  return Unsafe.As<short, T>(ref v); }
            if (targetType == typeof(ushort)) { var v = (ushort)num; return Unsafe.As<ushort, T>(ref v); }
            if (targetType == typeof(byte))   { var v = (byte)num;   return Unsafe.As<byte, T>(ref v); }
            if (targetType == typeof(sbyte))  { var v = (sbyte)num;  return Unsafe.As<sbyte, T>(ref v); }
            if (targetType == typeof(double)) { return Unsafe.As<double, T>(ref num); }
        }

        return default!;
    }

}