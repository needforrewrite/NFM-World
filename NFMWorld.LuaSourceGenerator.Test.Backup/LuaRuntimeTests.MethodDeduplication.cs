using LuaJIT;
using static LuaJIT.Methods;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NFMWorld.LuaSourceGenerator.Test;

public partial class LuaRuntimeTests
{
    [TestMethod]
    public void MethodDedup_InterfaceImplementation_WorksCorrectly()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            local sum = calc:add(5, 3)
            local product = calc:multiply(4, 7)
            return sum, product
        ");

        AssertLuaOk(result);
        var sum = lua_tointeger(_L, -2);
        var product = lua_tointeger(_L, -1);
        Assert.AreEqual(8, sum);
        Assert.AreEqual(28, product);
    }

    [TestMethod]
    public void MethodDedup_VirtualOverride_WorksCorrectly()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            local diff = calc:subtract(10, 3)
            local quotient = calc:divide(20, 4)
            return diff, quotient
        ");

        AssertLuaOk(result);
        var diff = lua_tointeger(_L, -2);
        var quotient = lua_tointeger(_L, -1);
        Assert.AreEqual(7, diff);
        Assert.AreEqual(5, quotient);
    }

    [TestMethod]
    public void MethodDedup_UniqueMethod_WorksCorrectly()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            return calc:square(7)
        ");

        AssertLuaOk(result);
        var square = lua_tointeger(_L, -1);
        Assert.AreEqual(49, square);
    }

    [TestMethod]
    public void MethodDedup_InterfaceMethod_ReturnsString()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            return calc:getDescription()
        ");

        AssertLuaOk(result);
        var desc = lua_tostring(_L, -1);
        Assert.AreEqual("Calculator implementation", desc);
    }

    [TestMethod]
    public void MethodDedup_VirtualOverride_ReturnsString()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            return calc:getName()
        ");

        AssertLuaOk(result);
        var name = lua_tostring(_L, -1);
        Assert.AreEqual("DerivedCalculator", name);
    }

    [TestMethod]
    public void MethodDedup_DifferentTypesSameInterface_HaveDifferentImplementations()
    {
        var result = luaL_dostring(_L, @"
            local calc1 = TypeWithMethodDeduplication.new()
            local calc2 = AnotherCalculator.new()
            local sum1 = calc1:add(5, 3)
            local sum2 = calc2:add(5, 3)
            return sum1, sum2
        ");

        AssertLuaOk(result);
        var sum1 = lua_tointeger(_L, -2);
        var sum2 = lua_tointeger(_L, -1);
        Assert.AreEqual(8, sum1);
        Assert.AreEqual(9, sum2); // AnotherCalculator adds 1
    }

    [TestMethod]
    public void MethodDedup_NewMember_HasOwnImplementation()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithNewMember.new()
            return calc:subtract(10, 3)
        ");

        AssertLuaOk(result);
        var diff = lua_tointeger(_L, -1);
        Assert.AreEqual(14, diff); // (10 - 3) * 2 = 14, not 7
    }

    [TestMethod]
    public void MethodDedup_NewMember_OverrideUsesBaseImplementation()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithNewMember.new()
            return calc:divide(20, 4)
        ");

        AssertLuaOk(result);
        var quotient = lua_tointeger(_L, -1);
        Assert.AreEqual(5, quotient);
    }

    [TestMethod]
    public void MethodDedup_InterfaceCast_StillWorks()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            -- Even though we're calling on TypeWithMethodDeduplication,
            -- it should work because it implements ICalculator
            return calc:add(100, 50)
        ");

        AssertLuaOk(result);
        var sum = lua_tointeger(_L, -1);
        Assert.AreEqual(150, sum);
    }

    [TestMethod]
    public void MethodDedup_ExceptionPropagation_WorksCorrectly()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            local success, err = pcall(function()
                return calc:divide(10, 0)
            end)
            return success, err
        ");

        AssertLuaOk(result);
        var success = lua_toboolean(_L, -2) != 0;
        var error = lua_tostring(_L, -1);
        Assert.IsFalse(success);
        Assert.IsTrue(error!.Contains("DivideByZeroException"));
    }

    [TestMethod]
    public void MethodDedup_AllMethodsAccessible_ViaIndex()
    {
        var result = luaL_dostring(_L, @"
            local calc = TypeWithMethodDeduplication.new()
            -- Verify all methods are accessible
            local has_add = calc.add ~= nil
            local has_subtract = calc.subtract ~= nil
            local has_multiply = calc.multiply ~= nil
            local has_divide = calc.divide ~= nil
            local has_square = calc.square ~= nil
            local has_getName = calc.getName ~= nil
            local has_getDescription = calc.getDescription ~= nil
            return has_add, has_subtract, has_multiply, has_divide, has_square, has_getName, has_getDescription
        ");

        AssertLuaOk(result);
        for (int i = -7; i <= -1; i++)
        {
            Assert.IsTrue(lua_toboolean(_L, i) != 0, $"Method at index {i} should be accessible");
        }
    }
}
