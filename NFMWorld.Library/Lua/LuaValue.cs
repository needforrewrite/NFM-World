using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace nfm_world_library.Lua;

public abstract class LuaValue(lua_State L) : IDisposable
{
    protected readonly lua_State L = L;
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
                return new LuaBoolean(L, boolVal);
            case LUA_TNUMBER:
                var num = lua_tonumber(L, idx);
                return new LuaNumber(L, num);
            case LUA_TSTRING:
                var str = lua_tostring(L, idx);
                return new LuaString(L, str!);
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
                return new LuaLightUserdata(L, (nint)ptr);
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

    public abstract void Push();
}

public class LuaThread(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public override string ToString()
    {
        return $"LuaThread(Reference={Reference})";
    }
}

public class LuaLightUserdata(lua_State L, nint ptr) : LuaValue(L)
{
    public nint Value { get; } = ptr;

    public override string ToString()
    {
        return $"LuaLightUserdata(Value=0x{Value:X})";
    }

    public override unsafe void Push()
    {
        lua_pushlightuserdata(L, (void*) Value);
    }
}

public class LuaFunction(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public override string ToString()
    {
        return $"LuaFunction(Reference={Reference})";
    }
    
    public void Call(params ReadOnlySpan<LuaValue> args)
    {
        Push();
        foreach (var arg in args)
        {
            arg.Push();
        }
        if (lua_pcall(L, args.Length, 0, 0) != LUA_OK)
        {
            var error = lua_tostring(L, -1);
            lua_pop(L, 1);
            throw new Exception($"Lua function call failed: {error}");
        }
    }
}

public class LuaTable(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public LuaValue this[LuaValue key]
    {
        get
        {
            Push();
            key.Push();
            lua_gettable(L, -2);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value!;
        }
        set
        {
            Push();
            key.Push();
            value.Push();
            lua_settable(L, -3);
            lua_pop(L, 1);
        }
    }
    
    public LuaValue this[string key]
    {
        get
        {
            Push();
            lua_getfield(L, -1, key);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value!;
        }
        set
        {
            Push();
            value.Push();
            lua_setfield(L, -2, key);
            lua_pop(L, 1);
        }
    }
    
    public LuaValue this[double index]
    {
        get
        {
            Push();
            lua_pushnumber(L, index);
            lua_gettable(L, -2);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value!;
        }
        set
        {
            Push();
            lua_pushnumber(L, index);
            value.Push();
            lua_settable(L, -3);
            lua_pop(L, 1);
        }
    }
    
    public LuaValue this[bool index]
    {
        get
        {
            Push();
            lua_pushboolean(L, index ? 1 : 0);
            lua_gettable(L, -2);
            var value = Create(L, -1);
            lua_pop(L, 2);
            return value!;
        }
        set
        {
            Push();
            lua_pushboolean(L, index ? 1 : 0);
            value.Push();
            lua_settable(L, -3);
            lua_pop(L, 1);
        }
    }
    
    public int Length
    {
        get
        {
            Push();
            int len = (int)lua_objlen(L, -1);
            lua_pop(L, 1);
            return len;
        }
    }
    
    public override string ToString()
    {
        return $"LuaTable(Reference={Reference})";
    }
}

public class LuaUserdata(lua_State L, int luaLRef) : LuaReferenceValue(L, luaLRef)
{
    public override string ToString()
    {
        return $"LuaUserdata(Reference={Reference})";
    }
}

public class LuaBoolean(lua_State L, bool b) : LuaValue(L)
{
    public bool Value { get; } = b;

    public override string ToString()
    {
        return $"LuaBoolean(Value={Value})";
    }

    public override void Push()
    {
        lua_pushboolean(L, Value ? 1 : 0);
    }
    
    public static implicit operator bool(LuaBoolean luaBoolean) => luaBoolean.Value;
}

public class LuaNumber(lua_State L, double num) : LuaValue(L)
{
    public double Value { get; } = num;

    public override string ToString()
    {
        return $"LuaNumber(Value={Value.ToString(CultureInfo.InvariantCulture)})";
    }

    public override void Push()
    {
        lua_pushnumber(L, Value);
    }
    
    public static implicit operator double(LuaNumber luaNumber) => luaNumber.Value;
}

public class LuaString(lua_State L, string str) : LuaValue(L)
{
    public string Value { get; } = str;

    public override string ToString()
    {
        return $"LuaString(Value=\"{Value}\")";
    }

    public override void Push()
    {
        lua_pushstring(L, Value);
    }
    
    public static implicit operator string(LuaString luaString) => luaString.Value;
}

public abstract class LuaReferenceValue(lua_State L, int reference) : LuaValue(L), IDisposable
{
    public int Reference { get; } = reference;

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
        GC.SuppressFinalize(this);
        luaL_unref(L, LUA_REGISTRYINDEX, Reference);
    }
    
    public override void Push()
    {
        lua_rawgeti(L, LUA_REGISTRYINDEX, Reference);
    }
}
