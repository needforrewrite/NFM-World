using Microsoft.VisualStudio.TestTools.UnitTesting;
using NFMWorld.LuaSourceGenerator.Test.Bindings;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using LuaJIT;
using static LuaJIT.Methods;

namespace NFMWorld.LuaSourceGenerator.Test;

[TestClass]
public class LuaRuntimeTests_TypeWithOverloads
{
    private lua_State _L;

    [TestInitialize]
    public void Setup()
    {
        LuaBindings.Reset();
        _L = luaL_newstate();
        luaL_openlibs(_L);
        LuaBindings.Initialize(_L);
    }

    [TestCleanup]
    public void Cleanup()
    {
        lua_close(_L);
    }

    private void AssertLuaOk(int result)
    {
        if (result != 0)
        {
            var error = lua_tostring(_L, -1);
            Assert.Fail($"Lua error: {error}");
        }
    }

    #region Constructor Overloads

    [TestMethod]
    public void Constructor_IntOverload_CreatesCorrectInstance()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(42)
            return obj.value, obj.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(42, value);
        Assert.AreEqual("int", text);
    }

    [TestMethod]
    public void Constructor_FloatOverload_CreatesCorrectInstance()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(3.14)
            return obj.value, obj.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(3, value); // Truncated from 3.14
        Assert.AreEqual("float", text);
    }

    [TestMethod]
    public void Constructor_StringOverload_CreatesCorrectInstance()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new('hello')
            return obj.value, obj.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(5, value); // Length of 'hello'
        Assert.AreEqual("string:hello", text);
    }

    #endregion

    #region Method Overloads - Single Parameter

    [TestMethod]
    public void ProcessNumber_IntOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:processNumber(42)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("int:42", text);
    }

    [TestMethod]
    public void ProcessNumber_DoubleOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:processNumber(3.14159)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("double:3.14159", text);
    }

    [TestMethod]
    public void ProcessNumber_LongOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            -- Lua numbers are doubles, but large integers should prefer long
            return obj:processNumber(2147483648)  -- Beyond int range
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.IsTrue(text.StartsWith("long:") || text.StartsWith("double:"));
    }

    [TestMethod]
    public void ProcessData_StringOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:processData('test')
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("string:test", text);
    }

    [TestMethod]
    public void ProcessData_IntArrayOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:processData({1, 2, 3, 4, 5})
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("int[]:5", text);
    }

    [TestMethod]
    public void ProcessData_FloatArrayOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:processData({1.1, 2.2, 3.3})
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        // Arrays are ambiguous - should default to first matching type (int[])
        Assert.IsTrue(text == "int[]:3" || text == "float[]:3");
    }

    [TestMethod]
    public void ProcessData_BoolOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:processData(true)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("bool:True", text);
    }

    #endregion

    #region Method Overloads - Two Parameters

    [TestMethod]
    public void Combine_IntInt_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:combine(10, 20)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("int,int:10,20", text);
    }

    [TestMethod]
    public void Combine_FloatFloat_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:combine(1.5, 2.5)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("float,float:1.5,2.5", text);
    }

    [TestMethod]
    public void Combine_StringString_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:combine('hello', 'world')
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("string,string:hello,world", text);
    }

    [TestMethod]
    public void Combine_IntString_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:combine(42, 'answer')
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("int,string:42,answer", text);
    }

    [TestMethod]
    public void Combine_StringInt_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(0)
            return obj:combine('answer', 42)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("string,int:answer,42", text);
    }

    #endregion

    #region Static Method Overloads

    [TestMethod]
    public void StaticProcess_IntOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            return TypeWithOverloads.staticProcess(42)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("static:int:42", text);
    }

    [TestMethod]
    public void StaticProcess_DoubleOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            return TypeWithOverloads.staticProcess(3.14159)
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("static:double:3.14159", text);
    }

    [TestMethod]
    public void StaticProcess_StringOverload_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            return TypeWithOverloads.staticProcess('test')
        ");
        AssertLuaOk(result);

        var text = lua_tostring(_L, -1);
        Assert.AreEqual("static:string:test", text);
    }

    #endregion

    #region Operator Overloads

    [TestMethod]
    public void UnaryMinus_ReturnsNegatedValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(42)
            local result = -obj
            return result.value
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(-42, value);
    }

    [TestMethod]
    public void Addition_ObjectPlusObject_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj1 = TypeWithOverloads.new(10)
            local obj2 = TypeWithOverloads.new(20)
            local result = obj1 + obj2
            return result.value, result.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(30, value);
        Assert.AreEqual("obj+obj", text);
    }

    [TestMethod]
    public void Addition_ObjectPlusInt_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(10)
            local result = obj + 5
            return result.value, result.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(15, value);
        Assert.AreEqual("obj+int", text);
    }

    [TestMethod]
    public void Addition_IntPlusObject_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(10)
            local result = 5 + obj
            return result.value, result.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(15, value);
        Assert.AreEqual("int+obj", text);
    }

    [TestMethod]
    public void Subtraction_ObjectMinusObject_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj1 = TypeWithOverloads.new(20)
            local obj2 = TypeWithOverloads.new(8)
            local result = obj1 - obj2
            return result.value, result.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(12, value);
        Assert.AreEqual("obj-obj", text);
    }

    [TestMethod]
    public void Subtraction_ObjectMinusInt_ReturnsCorrectResult()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithOverloads.new(20)
            local result = obj - 5
            return result.value, result.text
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -2);
        var text = lua_tostring(_L, -1);

        Assert.AreEqual(15, value);
        Assert.AreEqual("obj-int", text);
    }

    #endregion
}
