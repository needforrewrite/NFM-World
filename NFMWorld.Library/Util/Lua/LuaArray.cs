using System.Collections;
using System.Runtime.CompilerServices;
using FixedMathSharp;
using Lua;
using Lua.Runtime;
using Maxine.Extensions.Collections;
using MemoryPack;
using NFMWorld.LuaSourceGenerator.Generator;
using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Util;

/// <summary>
/// A lua-compatible T[]
/// </summary>
/// <typeparam name="T">
/// The type of the array's elements. Must implement <see cref="ILuaUserData"/> or be a primitive
/// type for correct functionality.
/// </typeparam>
[LuaShimType("{ [integer]: T }")]
[MemoryPackable(GenerateType.Collection)]
public partial class LuaArray<T> : ILuaUserData, IList<T>, IReadOnlyList<T>
{
    public readonly IList<T> Value;

    public LuaArray()
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
    public LuaArray(int length)
    {
        Value = new T[length];
    }

    public LuaArray(IList<T> innerList)
    {
        Value = innerList;
    }

    public LuaArray(InlineArray2<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray3<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray4<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray5<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray6<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray7<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray8<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray9<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray10<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray11<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray12<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray13<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray14<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray15<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray16<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray2Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray3Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray4Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray5Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray6Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray7Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray8Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray9Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray10Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray11Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray12Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray13Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray14Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray15Ex<T> innerList) => Value = [..innerList];
    public LuaArray(InlineArray16Ex<T> innerList) => Value = [..innerList];

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

    private static ValueTask<int> NewIndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaArray<T>>(0);
        var key = context.GetArgument(1);
        var value = context.GetArgument(2);

        // Integer key → array index (Lua is 1-indexed)
        if (key.TryRead<double>(out var num) && LuaHelpers.IsLuaIndex(num, out var index))
        {
            if (!value.TryRead<T>(out var typedValue))
            {
                // Fallback: try number → T conversion for common numeric types
                typedValue = LuaHelpers.ConvertLuaValue<T>(value);
            }
            arr[index] = typedValue;
        }

        return new(context.Return());
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaArray<T>>(0);
        return new(context.Return((double)arr.Value.Count));
    }

    public int IndexOf(T item) => Value.IndexOf(item);
    void IList<T>.Insert(int index, T item) => Value.Insert(index, item);
    void IList<T>.RemoveAt(int index) => Value.RemoveAt(index);

    public IEnumerator<T> GetEnumerator() => Value.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    void ICollection<T>.Add(T item) => Value.Add(item);
    void ICollection<T>.Clear() => Value.Clear();
    bool ICollection<T>.Contains(T item) => Value.Contains(item);
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => Value.CopyTo(array, arrayIndex);
    bool ICollection<T>.Remove(T item) => Value.Remove(item);
    bool ICollection<T>.IsReadOnly => Value.IsReadOnly;

    public int Count => Value.Count;

    public static implicit operator LuaArray<T>(T[] arr) => new(arr);
}