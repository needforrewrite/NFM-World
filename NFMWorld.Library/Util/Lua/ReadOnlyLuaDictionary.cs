using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Lua;
using Lua.Runtime;
using nfm_world_library.Lua;

namespace NFMWorldLibrary.Util;

[LuaShimType("{ [TKey]: TValue }")]
public class ReadOnlyLuaDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> value)
    : ILuaUserData, IReadOnlyDictionary<TKey, TValue>
{
    public readonly IReadOnlyDictionary<TKey, TValue> Value = value;

    public TValue this[TKey key]
    {
        get => Value[key];
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
            mt[Metamethods.Pairs] = Pairs;
            mt[Metamethods.IPairs] = Ipairs;

            Interlocked.CompareExchange(ref field, mt, null);
            return field!;
        }
    }

    private static ValueTask<int> IndexMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaDictionary<TKey, TValue>>(0);
        var key = context.GetArgument(1);

        if (key.TryRead<TKey>(out var typedValue))
        {
            return new(context.Return(LuaHelpers.ToLuaValue(arr[typedValue]!)));
        }

        return new(context.Return(LuaValue.Nil));
    }

    private static ValueTask<int> LenMetamethodImpl(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var arr = context.GetArgument<LuaDictionary<TKey, TValue>>(0);
        return new(context.Return((double)arr.Value.Count));
    }

    private static readonly Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> Inext = static (context, ct) =>
    {
        var instance = context.GetCsClosure()!.UpValues[0].Read<IEnumerator<KeyValuePair<TKey, TValue>>>();
        var idx = context.GetCsClosure()!.UpValues[1].Read<int>();
        context.GetCsClosure()!.UpValues[1] = idx + 1;
        if (!instance.MoveNext())
        {
            return ValueTask.FromResult(context.Return());
        }

        var t = new LuaTable();
        t[1] = LuaHelpers.ToLuaValue(instance.Current.Key);
        t[2] = LuaHelpers.ToLuaValue(instance.Current.Value);
        
        return ValueTask.FromResult(context.Return(idx + 1, t));
    };

    private static readonly LuaFunction Ipairs = new(Metamethods.IPairs, (context, ct) =>
    {
        var instance = context.GetArgument<IEnumerable<KeyValuePair<TKey, TValue>>>(0);
        
        // upvalues: instance, idx
        var closure = new CSharpClosure("inext", [LuaValue.FromLightUserData(instance), 0], Inext);
        
        return ValueTask.FromResult(context.Return(closure, LuaValue.Nil, LuaValue.Nil));
    });
        
    // pairs
    private static readonly Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> Next = static (context, ct) =>
    {
        var instance = context.GetCsClosure()!.UpValues[0].Read<IEnumerator<KeyValuePair<TKey, TValue>>>();
        if (!instance.MoveNext())
        {
            return ValueTask.FromResult(context.Return());
        }
        
        return ValueTask.FromResult(context.Return(LuaHelpers.ToLuaValue(instance.Current.Key), LuaHelpers.ToLuaValue(instance.Current.Value)));
    };

    private static readonly LuaFunction Pairs = new(Metamethods.Pairs, (context, ct) =>
    {
        var instance = context.GetArgument<IEnumerable<KeyValuePair<TKey, TValue>>>(0);
        
        // upvalues: instance, idx
        var closure = new CSharpClosure("next", [LuaValue.FromLightUserData(instance)], Next);
        
        return ValueTask.FromResult(context.Return(closure, LuaValue.Nil, LuaValue.Nil));
    });

    public bool ContainsKey(TKey key)
    {
        return Value.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return Value.TryGetValue(key, out value);
    }

    public IEnumerable<TKey> Keys => Value.Keys;

    public IEnumerable<TValue> Values => Value.Values;

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return Value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Value).GetEnumerator();
    }

    public int Count => Value.Count;
}