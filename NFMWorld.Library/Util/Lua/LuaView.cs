using System.Runtime.CompilerServices;
using Lua;
using Lua.Runtime;
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
/// <param name="reverseFactory">Converts from TView back to T for writebacks</param>
[LuaShimType("{ [integer]: TView }")]
public class LuaView<T, TView>(IList<T> innerList, Func<T, TView> factory, Func<TView, T> reverseFactory) : ILuaUserData
{
    public readonly IList<T> Value = innerList;

    public T this[int index]
    {
        get => Value[index];
        set => Value[index] = value;
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
            mt[Metamethods.NewIndex] = new LuaFunction("__newindex", NewIndexMetamethodImpl);
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
        if (key.TryRead<double>(out var num) && LuaHelpers.IsLuaIndex(num, out var index))
        {
            if ((uint)index < (uint)arr.Value.Count)
            {
                return new(context.Return(LuaHelpers.ToLuaValue(LuaProxies.GetOrAdd(arr[index]!, factory))));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    private ValueTask<int> NewIndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaView<T, TView>>(0);
        var key = context.GetArgument(1);
        var value = context.GetArgument(2);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && LuaHelpers.IsLuaIndex(num, out var index))
        {
            if (!value.TryRead<TView>(out var typedValue))
            {
                // Fallback: try number → T conversion for common numeric types
                typedValue = LuaHelpers.ConvertLuaValue<TView>(value);
            }
            arr[index] = LuaProxies.GetOrAdd(typedValue, reverseFactory);
        }

        return new(context.Return());
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaView<T, TView>>(0);
        return new(context.Return((double)arr.Value.Count));
    }

}