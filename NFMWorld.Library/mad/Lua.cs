// ReSharper disable InconsistentNaming

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Mad;

public partial class Lua
{
    private static ulong _maxId;
    private static readonly Dictionary<ulong, object> _objects = new();
    private static readonly Dictionary<object, ulong> _objectsReverse = new();
    
    private static ulong GetObjectId(object obj)
    {
        if (_objectsReverse.TryGetValue(obj, out var id))
        {
            return id;
        }
        _maxId++;
        _objects[_maxId] = obj;
        _objectsReverse[obj] = _maxId;
        return _maxId;
    }
    
    private static void NewObjectUserdata(lua_State L, object obj)
    {
        var ptr = lua_newuserdata(L, 8);
        Marshal.WriteInt64((IntPtr)ptr, (long)GetObjectId(obj));
    }

    private static T ReadObjectUserdata<T>(lua_State L)
    {
        var phasePtr = lua_touserdata(L, -1);
        var phaseId = (ulong)Marshal.ReadInt64((IntPtr)phasePtr);
        var phase = (T)_objects[phaseId]!;
        return phase;
    }
    
    // public void PushFunctions(BaseRacePhase phase, lua_State L)
    // {
    //     // add car metatable
    //     luaL_newmetatable(L, "CarMetaTable");
    //     lua_pushcfunction(L, static (L) =>
    //     {
    //         // __index function for car metatable
    //         var carPtr = lua_touserdata(L, 1);
    //         var carId = (ulong)Marshal.ReadInt64((IntPtr)carPtr);
    //         var car = (InGameCar)_objects[carId]!;
    //         var field = lua_tostring(L, 2);
    //         switch (field)
    //         {
    //         }
    //         lua_pushnil(L);
    //         return 1;
    //     });
    //     lua_setfield(L, -2, "__index");
    //     lua_pushcfunction(L, static (L) =>
    //     {
    //         // __newindex function for car metatable
    //         var carPtr = lua_touserdata(L, 1);
    //         var carId = (ulong)Marshal.ReadInt64((IntPtr)carPtr);
    //         var car = (InGameCar)_objects[carId]!;
    //         var field = lua_tostring(L, 2);
    //         switch (field)
    //         {
    //         }
    //         return 0;
    //     });
    //     lua_setfield(L, -2, "__newindex");
    //     lua_pop(L, 1); // pop metatable
    //
    //     // add cars table to Lua. it contains a metatable with __index function to get cars by id
    //     lua_newtable(L); // cars table
    //     lua_newtable(L); // metatable
    //     lua_pushcfunction(L, static (L) =>
    //     {
    //         // __index function
    //         var carId = (int)lua_tointeger(L, 2);
    //         lua_getglobal(L, "phase");
    //         var phase = ReadObjectUserdata<BaseRacePhase>(L);
    //         lua_pop(L, 1); // pop phase userdata
    //         var car = phase.CarsInRace[carId];
    //         if (car != null!)
    //         {
    //             // return car userdata with metatable
    //             NewObjectUserdata(L, (long)GetObjectId(car));
    //             luaL_getmetatable(L, "CarMetaTable");
    //             lua_setmetatable(L, -2);
    //             return 1;
    //         }
    //         lua_pushnil(L);
    //         return 1;
    //     });
    //     lua_setfield(L, -2, "__index");
    //     lua_setmetatable(L, -2);
    //     lua_setglobal(L, "cars");
    //     
    //     // phase global
    //     NewObjectUserdata(L, phase);
    //     lua_setglobal(L, "phase");
    // }

    private void PushFix64InlineArray4(lua_State L, Mad mad, Func<Mad, InlineArray4<fix64>> getter)
    {
        throw new NotImplementedException();
    }

    private UnlimitedArray<bool> ReadBoolUnlimitedArray(lua_State L, int idx)
    {
        var length = (int)lua_objlen(L, idx);
        var array = new UnlimitedArray<bool>(length);
        for (var i = 0; i < length; i++)
        {
            lua_rawgeti(L, idx, i + 1);
            array[i] = lua_toboolean(L, -1) != 0;
            lua_pop(L, 1);
        }
        return array;
    }

    private int[,] ReadInt2DArray(lua_State L, int idx)
    {
        var outerLength = (int)lua_objlen(L, idx);
        lua_rawgeti(L, idx, 1);
        var innerLength = (int)lua_objlen(L, -1);
        lua_pop(L, 1);
        var array = new int[outerLength, innerLength];
        for (var i = 0; i < outerLength; i++)
        {
            lua_rawgeti(L, idx, i + 1);
            for (var j = 0; j < innerLength; j++)
            {
                lua_rawgeti(L, -1, j + 1);
                array[i, j] = (int)lua_tointeger(L, -1);
                lua_pop(L, 1);
            }
            lua_pop(L, 1);
        }
        return array;
    }

    private InlineArray4<fix64> ReadFix64InlineArray4(lua_State L, int idx)
    {
        var array = new InlineArray4<fix64>();
        for (var i = 0; i < 4; i++)
        {
            lua_rawgeti(L, idx, i + 1);
            // array[i] = (fix64)SoftFloat.f64_to_f64(lua_tonumber(L, -1));
            lua_pop(L, 1);
        }
        return array;
    }

    private void PushBoolUnlimitedArray(lua_State L, UnlimitedArray<bool> arr)
    {
        throw new NotImplementedException();
    }

    private void PushInt2DArray(lua_State L, int[,] arr)
    {
        throw new NotImplementedException();
    }
}