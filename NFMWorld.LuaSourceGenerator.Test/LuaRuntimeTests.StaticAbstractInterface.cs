using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.LuaSourceGenerator.Test.Bindings;
using LuaJIT;
using static LuaJIT.Methods;

namespace NFMWorld.LuaSourceGenerator.Test;

public partial class LuaRuntimeTests
{
    #region Static Abstract Interface Tests

    [TestMethod]
    public void StaticAbstractInterface_Parse_ValidInput()
    {
        // Test that static abstract interface implementation can be called as a static method
        var result = luaL_dostring(_L, @"
            local obj = TypeWithStaticAbstractInterface.parse('42')
            return obj.value
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void StaticAbstractInterface_Parse_InvalidInput()
    {
        // Test that static abstract interface implementation throws on invalid input
        var result = luaL_dostring(_L, @"
            local success, err = pcall(function()
                return TypeWithStaticAbstractInterface.parse('not a number')
            end)
            return success
        ");

        AssertLuaOk(result);
        var success = lua_tointeger(_L, -1) != 0;
        Assert.IsFalse(success); // Should have thrown an exception
    }

    [TestMethod]
    public void StaticAbstractInterface_Parse_EmptyInput()
    {
        // Test that static abstract interface implementation throws on empty input
        var result = luaL_dostring(_L, @"
            local success, err = pcall(function()
                return TypeWithStaticAbstractInterface.parse('')
            end)
            return success, err
        ");

        AssertLuaOk(result);
        var success = lua_tointeger(_L, -1) != 0;
        lua_pop(_L, 1);
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void StaticAbstractInterface_StaticProperty_Zero()
    {
        // Test static abstract property implementation
        var result = luaL_dostring(_L, @"
            local zero = TypeWithStaticAbstractInterface.zero
            return zero.value
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void StaticAbstractInterface_RegularStaticMethod()
    {
        // Test that regular static methods still work
        var result = luaL_dostring(_L, @"
            local obj = TypeWithStaticAbstractInterface.fromDouble(42.7)
            return obj.value
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void StaticAbstractInterface_InstanceMethod()
    {
        // Test that instance methods work correctly (not confused with static methods)
        var result = luaL_dostring(_L, @"
            local obj = TypeWithStaticAbstractInterface.new(10)
            return obj:add(5)
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(15, value);
    }

    [TestMethod]
    public void StaticAbstractInterface_CannotCallParseAsInstanceMethod()
    {
        // Verify that static abstract methods are NOT accessible as instance methods
        var result = luaL_dostring(_L, @"
            local obj = TypeWithStaticAbstractInterface.new(10)
            -- This should fail because parse is a static method, not an instance method
            local success, err = pcall(function()
                return obj:parse('42')
            end)
            return success
        ");

        AssertLuaOk(result);
        var success = lua_tointeger(_L, -1) != 0;
        Assert.IsFalse(success); // Should fail because parse is static
    }

    [TestMethod]
    public void StaticAbstractInterface_MultipleTypes_IndependentImplementations()
    {
        // Verify that different types with static abstract implementations work independently
        var result = luaL_dostring(_L, @"
            local obj1 = TypeWithStaticAbstractInterface.parse('100')
            local obj2 = TypeWithStaticAbstractInterface.parse('200')
            return obj1.value, obj2.value
        ");

        AssertLuaOk(result);
        var value2 = lua_tointeger(_L, -1);
        lua_pop(_L, 1);
        var value1 = lua_tointeger(_L, -1);

        Assert.AreEqual(100, value1);
        Assert.AreEqual(200, value2);
    }

    #endregion
}
