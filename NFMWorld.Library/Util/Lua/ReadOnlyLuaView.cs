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
    private readonly Func<T, TView> _factory = factory;

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
    private static LuaTable SharedMetatable
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

    private static ValueTask<int> IndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<ReadOnlyLuaView<T, TView>>(0);
        var key = context.GetArgument(1);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && IsLuaIndex(num, out var index))
        {
            if ((uint)index < (uint)arr.Value.Count)
            {
                return new(context.Return(LuaHelpers.ToLuaValue(LuaProxies.GetOrAdd(arr[index]!, arr._factory))));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<ReadOnlyLuaView<T, TView>>(0);
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