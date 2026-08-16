using System.Collections;
using System.Runtime.CompilerServices;
using Lua;
using Lua.Runtime;
using Maxine.Extensions.Collections;
using MemoryPack;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util;

[LuaShimType("{ [integer]: T }")]
public partial class ReadOnlyLuaArray<T> : ILuaUserData, IReadOnlyList<T>
{
    public readonly IReadOnlyList<T> Value;

    public ReadOnlyLuaArray()
    {
        Value = new List<T>();
    }

    /// <summary>
    /// A lua-compatible T[]
    /// </summary>
    /// <param name="length">The length of the array</param>
    /// <typeparam name="T">
    /// The type of the array's elements. Must implement <see cref="ILuaUserData"/> or be a primitive
    /// type for correct functionality.
    /// </typeparam>
    public ReadOnlyLuaArray(int length)
    {
        Value = new T[length];
    }

    public ReadOnlyLuaArray(IReadOnlyList<T> innerList)
    {
        Value = innerList;
    }

    public ReadOnlyLuaArray(InlineArray2<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray3<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray4<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray5<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray6<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray7<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray8<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray9<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray10<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray11<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray12<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray13<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray14<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray15<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray16<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray2Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray3Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray4Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray5Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray6Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray7Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray8Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray9Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray10Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray11Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray12Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray13Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray14Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray15Ex<T> innerList) => Value = [..innerList];
    public ReadOnlyLuaArray(InlineArray16Ex<T> innerList) => Value = [..innerList];

    public T this[int index] => Value[index];

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
        var arr = context.GetArgument<LuaArray<T>>(0);
        var key = context.GetArgument(1);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && LuaHelpers.IsLuaIndex(num, out var index))
        {
            if ((uint)index < (uint)arr.Value.Count)
            {
                return new(context.Return(LuaHelpers.ToLuaValue(arr[index]!)));
            }
        }

        return new(context.Return(LuaValue.Nil));
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaArray<T>>(0);
        return new(context.Return((double)arr.Value.Count));
    }

    public int IndexOf(T item) => Value.IndexOf(item);

    public IEnumerator<T> GetEnumerator() => Value.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => Value.Count;

    public static implicit operator ReadOnlyLuaArray<T>(T[] arr) => new(arr);
}