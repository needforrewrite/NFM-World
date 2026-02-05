using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static LuaJIT.Methods;
// ReSharper disable InconsistentNaming
// ReSharper disable ParameterHidesMember

namespace LuaJIT;

public abstract class LuaValue : IDisposable
{
    public override string ToString()
    {
        return "LuaValue";
    }

    public virtual void Dispose()
    {
    }

    public static unsafe LuaValue? Create(lua_State L, int idx)
    {
        int type = lua_type(L, idx);

        switch (type)
        {
            case LUA_TBOOLEAN:
                var boolVal = lua_toboolean(L, idx) != 0;
                return new LuaBoolean(boolVal);
            case LUA_TNUMBER:
                var num = lua_tonumber(L, idx);
                return new LuaNumber(num);
            case LUA_TSTRING:
                var str = lua_tostring(L, idx);
                return new LuaString(str!);
            case LUA_TTABLE:
                lua_pushvalue(L, idx);
                return new LuaTable(L, luaL_ref(L, LUA_REGISTRYINDEX));
            case LUA_TFUNCTION:
                lua_pushvalue(L, idx);
                return new LuaFunction(L, luaL_ref(L, LUA_REGISTRYINDEX));
            case LUA_TUSERDATA:
                lua_pushvalue(L, idx);
                return new LuaUserdata(L, luaL_ref(L, LUA_REGISTRYINDEX));
            case LUA_TLIGHTUSERDATA:
                var ptr = lua_touserdata(L, idx);
                return new LuaLightUserdata((nint)ptr);
            case LUA_TTHREAD:
                lua_pushvalue(L, idx);
                return new LuaThread(L, luaL_ref(L, LUA_REGISTRYINDEX));
            case LUA_TNIL:
                return null;
            default:
                ThrowArgumentOutOfRangeException(type);
                return null!;
        }
    }

    private static void ThrowArgumentOutOfRangeException(int type)
    {
        throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported Lua type");
    }

    public abstract void Push(lua_State L);
}

public sealed class LuaThread(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public override string ToString()
    {
        return $"LuaThread(Reference={Reference})";
    }
}

public sealed class LuaLightUserdata(nint ptr) : LuaValue
{
    public nint Value { get; } = ptr;

    public override string ToString()
    {
        return $"LuaLightUserdata(Value=0x{Value:X})";
    }

    public override unsafe void Push(lua_State L)
    {
        lua_pushlightuserdata(L, (void*) Value);
    }
}

public sealed class LuaFunction(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public override string ToString()
    {
        return $"LuaFunction(Reference={Reference})";
    }
    
    public void Call(params ReadOnlySpan<LuaValue> args)
    {
        Push(L);
        foreach (var arg in args)
        {
            arg.Push(L);
        }
        if (lua_pcall(L, args.Length, 0, 0) != LUA_OK)
        {
            var error = lua_tostring(L, -1);
            lua_pop(L, 1);
            throw new Exception($"Lua function call failed: {error}");
        }
    }
}

public sealed class LuaTable(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef), IDictionary<LuaValue, LuaValue>
{
    public void Add(LuaValue key, LuaValue? value)
    {
        Push(L);
        key.Push(L);
        PushSafe(L, value);
        lua_settable(L, -3);
        lua_pop(L, 1);
    }

    private static void PushSafe(lua_State L, LuaValue? value)
    {
        if (value is null)
        {
            lua_pushnil(L);
        }
        else
        {
            value.Push(L);
        }
    }

    public bool ContainsKey(LuaValue key)
    {
        Push(L);
        key.Push(L);
        lua_gettable(L, -2);
        var val = Create(L, -1);
        lua_pop(L, 2);
        return val is not null;
    }

    public bool Remove(LuaValue key)
    {
        Push(L);
        key.Push(L);
        lua_pushnil(L);
        lua_settable(L, -3);
        lua_pop(L, 1);
        return true;
    }

    public bool TryGetValue(LuaValue key, [MaybeNullWhen(false)] out LuaValue value)
    {
        Push(L);
        key.Push(L);
        lua_gettable(L, -2);
        var val = Create(L, -1);
        lua_pop(L, 2);
        if (val is null)
        {
            value = null;
            return false;
        }
        value = val;
        return true;
    }

    public LuaValue this[LuaValue key]
    {
        get
        {
            Push(L);
            key.Push(L);
            lua_gettable(L, -2);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value ?? throw new KeyNotFoundException($"Key '{key}' not found in LuaTable.");
        }
        set
        {
            Push(L);
            key.Push(L);
            PushSafe(L, value);
            lua_settable(L, -3);
            lua_pop(L, 1);
        }
    }

    public ICollection<LuaValue> Keys => new KeyCollection(this);

    private class KeyCollection(LuaTable table) : ICollection<LuaValue>
    {
        public IEnumerator<LuaValue> GetEnumerator()
        {
            foreach (var kvp in table)
            {
                yield return kvp.Key;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(LuaValue item)
        {
            throw new NotSupportedException("Collection is read-only.");
        }

        public void Clear()
        {
            throw new NotSupportedException("Collection is read-only.");
        }

        public bool Contains(LuaValue item)
        {
            return table.ContainsKey(item);
        }

        public void CopyTo(LuaValue[] array, int arrayIndex)
        {
            foreach (var key in this)
            {
                array[arrayIndex++] = key;
            }
        }

        public bool Remove(LuaValue item)
        {
            throw new NotSupportedException("Collection is read-only.");
        }

        public int Count => table.Count;
        public bool IsReadOnly => true;
    }

    public ICollection<LuaValue> Values => new ValueCollection(this);

    private class ValueCollection(LuaTable table) : ICollection<LuaValue>
    {
        public IEnumerator<LuaValue> GetEnumerator()
        {
            foreach (var kvp in table)
            {
                yield return kvp.Value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(LuaValue item)
        {
            throw new NotSupportedException("Collection is read-only.");
        }

        public void Clear()
        {
            throw new NotSupportedException("Collection is read-only.");
        }

        public bool Contains(LuaValue item)
        {
            foreach (var value in this)
            {
                if (Equals(value, item))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(LuaValue[] array, int arrayIndex)
        {
            foreach (var value in this)
            {
                array[arrayIndex++] = value;
            }
        }

        public bool Remove(LuaValue item)
        {
            throw new NotSupportedException("Collection is read-only.");
        }

        public int Count => table.Count;
        public bool IsReadOnly => true;
    }

    public LuaValue this[string key]
    {
        get
        {
            Push(L);
            lua_getfield(L, -1, key);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value ?? throw new KeyNotFoundException($"Key '{key}' not found in LuaTable.");
        }
        set
        {
            Push(L);
            PushSafe(L, value);
            lua_setfield(L, -2, key);
            lua_pop(L, 1);
        }
    }
    
    public LuaValue this[double index]
    {
        get
        {
            Push(L);
            lua_pushnumber(L, index);
            lua_gettable(L, -2);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value ?? throw new KeyNotFoundException($"Key '{index}' not found in LuaTable.");
        }
        set
        {
            Push(L);
            lua_pushnumber(L, index);
            PushSafe(L, value);
            lua_settable(L, -3);
            lua_pop(L, 1);
        }
    }
    
    public LuaValue this[bool b]
    {
        get
        {
            Push(L);
            lua_pushboolean(L, b ? 1 : 0);
            lua_gettable(L, -2);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value ?? throw new KeyNotFoundException($"Key '{b}' not found in LuaTable.");
        }
        set
        {
            Push(L);
            lua_pushboolean(L, b ? 1 : 0);
            value.Push(L);
            lua_settable(L, -3);
            lua_pop(L, 1);
        }
    }
    
    public int Count
    {
        get
        {
            Push(L);
            int len = (int)lua_objlen(L, -1);
            lua_pop(L, 1);
            return len;
        }
    }

    public IEnumerator<KeyValuePair<LuaValue, LuaValue>> GetEnumerator()
    {
        Push(L);
        lua_pushnil(L);
        while (lua_next(L, -2) != 0)
        {
            var key = Create(L, -2)!;
            var value = Create(L, -1)!;
            lua_pop(L, 1);
            yield return new KeyValuePair<LuaValue, LuaValue>(key, value);
        }
        lua_pop(L, 1);
    }

    public override string ToString()
    {
        return $"LuaTable(Reference={Reference})";
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(KeyValuePair<LuaValue, LuaValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        Push(L);
        lua_pushnil(L);
        while (lua_next(L, -2) != 0)
        {
            lua_pop(L, 1);
            lua_pushvalue(L, -1);
            lua_pushnil(L);
            lua_settable(L, -4);
        }
        lua_pop(L, 1);
    }

    public bool Contains(KeyValuePair<LuaValue, LuaValue> item)
    {
        if (TryGetValue(item.Key, out var value))
        {
            return Equals(value, item.Value);
        }
        return false;
    }

    public void CopyTo(KeyValuePair<LuaValue, LuaValue>[] array, int arrayIndex)
    {
        foreach (var kvp in this)
        {
            array[arrayIndex++] = kvp;
        }
    }

    public bool Remove(KeyValuePair<LuaValue, LuaValue> item)
    {
        if (Contains(item))
        {
            return Remove(item.Key);
        }
        return false;
    }

    public bool IsReadOnly => false;
}

public sealed class LuaUserdata(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public override string ToString()
    {
        return $"LuaUserdata(Reference={Reference})";
    }
}

public sealed class LuaBoolean(bool b) : LuaValue
{
    public bool Value { get; } = b;

    public override string ToString()
    {
        return $"LuaBoolean(Value={Value})";
    }

    public override void Push(lua_State L)
    {
        lua_pushboolean(L, Value ? 1 : 0);
    }
    
    public static implicit operator bool(LuaBoolean luaBoolean) => luaBoolean.Value;
    public static implicit operator LuaBoolean(bool b) => new(b);
}

public sealed class LuaNumber(double num) : LuaValue
{
    public double Value { get; } = num;

    public override string ToString()
    {
        return $"LuaNumber(Value={Value.ToString(CultureInfo.InvariantCulture)})";
    }

    public override void Push(lua_State L)
    {
        lua_pushnumber(L, Value);
    }
    
    public static implicit operator double(LuaNumber luaNumber) => luaNumber.Value;
    public static implicit operator LuaNumber(double num) => new(num);
}

public sealed class LuaString(string str) : LuaValue
{
    public string Value { get; } = str;

    public override string ToString()
    {
        return $"LuaString(Value=\"{Value}\")";
    }

    public override void Push(lua_State L)
    {
        lua_pushstring(L, Value);
    }
    
    public static implicit operator string(LuaString luaString) => luaString.Value;
    public static implicit operator LuaString(string str) => new(str);
}

public abstract class LuaReferenceValue(lua_State L, int reference) : LuaValue
{
    protected readonly lua_State L = L;
    private bool _disposed;
    public int Reference { get; private set; } = reference;

    public override string ToString()
    {
        return $"LuaReferenceValue(Reference={Reference})";
    }

    ~LuaReferenceValue()
    {
        Dispose();
    }
    
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
        luaL_unref(L, LUA_REGISTRYINDEX, Reference);
        Reference = LUA_NOREF;
    }
    
    public override void Push(lua_State L)
    {
        lua_rawgeti(L, LUA_REGISTRYINDEX, Reference);
    }
}
