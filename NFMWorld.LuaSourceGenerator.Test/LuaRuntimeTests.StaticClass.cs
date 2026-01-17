using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.LuaSourceGenerator.Test.Bindings;
using LuaJIT;
using static LuaJIT.Methods;

namespace NFMWorld.LuaSourceGenerator.Test;

public partial class LuaRuntimeTests
{
    #region Static Class Tests

    [TestMethod]
    public void StaticClass_CannotBeInstantiated()
    {
        // Static classes should not have a constructor
        var result = luaL_dostring(_L, @"
            local obj = StaticClass.new
            return obj
        ");

        // Should return nil (no constructor exists)
        AssertLuaOk(result);
        Assert.AreEqual(LUA_TNIL, lua_type(_L, -1));
    }

    [TestMethod]
    public void StaticClass_CallStaticMethod_NoParameters()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.getMagicNumber()
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(123, value);
    }

    [TestMethod]
    public void StaticClass_CallStaticMethod_WithParameters()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.add(10, 20)
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(30, value);
    }

    [TestMethod]
    public void StaticClass_CallStaticMethod_ReturningString()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.greet('World')
        ");

        AssertLuaOk(result);
        var value = lua_tostring(_L, -1);
        Assert.AreEqual("Hello, World!", value);
    }

    [TestMethod]
    public void StaticClass_CallStaticMethod_MultipleParameters()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.calculate(10, 5, 'mul')
        ");

        AssertLuaOk(result);
        var value = lua_tonumber(_L, -1);
        Assert.AreEqual(50.0, value, 0.001);
    }

    [TestMethod]
    public void StaticClass_AccessStaticProperty_Read()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.staticProperty
        ");

        AssertLuaOk(result);
        var value = lua_tostring(_L, -1);
        Assert.AreEqual("Initial", value);
    }

    [TestMethod]
    public void StaticClass_AccessStaticProperty_Write()
    {
        var result = luaL_dostring(_L, @"
            StaticClass.staticProperty = 'Modified'
            return StaticClass.staticProperty
        ");

        AssertLuaOk(result);
        var value = lua_tostring(_L, -1);
        Assert.AreEqual("Modified", value);
    }

    [TestMethod]
    public void StaticClass_AccessReadOnlyProperty()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.readOnlyProperty
        ");

        AssertLuaOk(result);
        var value = lua_tonumber(_L, -1);
        Assert.AreEqual(3.14159, value, 0.00001);
    }

    [TestMethod]
    public void StaticClass_AccessStaticField_Read()
    {
        var result = luaL_dostring(_L, @"
            return StaticClass.staticField
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void StaticClass_AccessStaticField_Write()
    {
        var result = luaL_dostring(_L, @"
            StaticClass.staticField = 100
            return StaticClass.staticField
        ");

        AssertLuaOk(result);
        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(100, value);
    }

    [TestMethod]
    public void StaticClass_StaticEvent_CanSubscribe()
    {
        var result = luaL_dostring(_L, @"
            local receivedMessage = nil
            StaticClass:add_OnMessage(function(msg)
                receivedMessage = msg
            end)
            StaticClass.raiseMessage('Test Message')
            return receivedMessage
        ");

        AssertLuaOk(result);
        var value = lua_tostring(_L, -1);
        Assert.AreEqual("Test Message", value);
    }

    [TestMethod]
    public void StaticClass_NoMetatableGenerated()
    {
        // Try to create an "instance" - should fail since there's no metatable
        var result = luaL_dostring(_L, @"
            local success, err = pcall(function()
                local obj = {}
                setmetatable(obj, StaticClass)
                return obj:getMagicNumber()
            end)
            return success
        ");

        AssertLuaOk(result);
        var success = lua_toboolean(_L, -1);
        Assert.IsFalse(success != 0, "Should not be able to use StaticClass as a metatable");
    }

    [TestMethod]
    public void StaticClass_TypeIsTable()
    {
        var result = luaL_dostring(_L, @"
            return type(StaticClass)
        ");

        AssertLuaOk(result);
        var typeStr = lua_tostring(_L, -1);
        Assert.AreEqual("table", typeStr);
    }

    #endregion
}
