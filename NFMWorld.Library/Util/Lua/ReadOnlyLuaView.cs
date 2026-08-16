using System.Runtime.CompilerServices;
using FixedMathSharp;
using Lua;
using Lua.Runtime;
using NFMWorld.LuaSourceGenerator.Generator;
using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Util;

/// <summary>
/// A lua-compatible list of <see cref="TView"/> backed by a list of <see cref="T"/>
/// </summary>
/// <typeparam name="T">
/// The type of the array's elements. 
/// </typeparam>
/// <typeparam name="TView">
/// The type of the userdata's elements as viewed from Lua. Must implement <see cref="ILuaUserData"/> or be a primitive
/// type for correct functionality.
/// </typeparam>
/// <param name="factory">Converts from T to TView</param>
[LuaShimType("{ [integer]: TView }")]
public class ReadOnlyLuaView<T, TView>(IReadOnlyList<T> innerList, Func<T, TView> factory) : ILuaUserData
{
    public readonly IReadOnlyList<T> Value = innerList;

    public T this[int index]
    {
        get => Value[index];
    }
    
    // ------------------------------------------------------------------
    // ILuaUserData — table-like behaviour via metatable
    // ------------------------------------------------------------------

    LuaTable? ILuaUserData.Metatable
    {
        get => field ??= SharedMetatable;
        set;
    }

    /// <summary>Shared metatable for all <see cref="UnlimitedArray{T}"/> instances of the same T.</summary>
    private LuaTable SharedMetatable
    {
        get
        {
            if (field != null)
                return field;

            var mt = new LuaTable(0, 3);
            mt[Metamethods.Index] = new LuaFunction("__index", IndexMetamethodImpl);
            mt[Metamethods.Len] = new LuaFunction("__len", LenMetamethodImpl);

            Interlocked.CompareExchange(ref field, mt, null);
            return field!;
        }
    }

    private ValueTask<int> IndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaView<T, TView>>(0);
        var key = context.GetArgument(1);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var index))
        {
            if ((uint)index < (uint)arr.Value.Count)
            {
                return new(context.Return(ToLuaValue(LuaProxies.GetOrAdd(arr[index]!, factory))));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    private static LuaValue ToLuaValue(TView value)
    {
        if (value is null) return LuaValue.Nil;
        
        if (value is bool @bool)
            return new LuaValue(@bool);
        if (value is float @float)
            return new LuaValue(@float);
        if (value is int @int)
            return new LuaValue(@int);
        if (value is long @long)
            return new LuaValue(@long);
        if (value is uint @uint)
            return new LuaValue(@uint);
        if (value is ulong @ulong)
            return new LuaValue(@ulong);
        if (value is short @short)
            return new LuaValue(@short);
        if (value is ushort @ushort)
            return new LuaValue(@ushort);
        if (value is byte @byte)
            return new LuaValue(@byte);
        if (value is sbyte @sbyte)
            return new LuaValue(@sbyte);
        if (value is double @double)
            return new LuaValue(@double);

        if (value is string @string)
            return new LuaValue(@string);

        if (value is LuaFunction func)
            return new LuaValue(func);
        if (value is LuaTable table)
            return new LuaValue(table);
        if (value is LuaState state)
            return new LuaValue(state);
        
        if (value is fix64 fixed64)
            return new LuaValue(fixed64);
        if (value is f64Vector3 f64Vector3)
            return new LuaValue(f64Vector3);
        if (value is f64AngleSingle f64AngleSingle)
            return new LuaValue(f64AngleSingle);
        if (value is f64Euler f64Euler)
            return new LuaValue(f64Euler);
        
        if (value is ILuaUserData userData)
            return LuaValue.FromUserData(userData);

        // Fallback!
        return LuaValue.FromObject(value);
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaView<T, TView>>(0);
        return new(context.Return((double)arr.Value.Count));
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
}