using LuaNET.LuaJIT;
using static LuaNET.LuaJIT.Lua;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using NFMWorld.LuaSourceGenerator.Test.Bindings;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Runtime integration tests that spawn a real LuaJIT runtime and verify
/// that generated bindings for SampleTypes work correctly from the Lua side.
/// </summary>
[TestClass]
public class LuaRuntimeTests
{
    private lua_State _L;

    [TestInitialize]
    public void Setup()
    {
        // Reset bindings state for test isolation
        LuaBindings.Reset();
        LuaBindings.ResetType<SampleClass>();
        LuaBindings.ResetType<SampleStruct>();
        LuaBindings.ResetType<Vector3Struct>();
        LuaBindings.ResetType<ReferencedType>();
        LuaBindings.ResetType<TypeWithReferences>();
        LuaBindings.ResetType<TypeWithArrays>();
        LuaBindings.ResetType<TypeWithMultiDimArray>();
        LuaBindings.ResetType<TypeWithIndexers>();

        // Create a new Lua state for each test
        _L = luaL_newstate();
        luaL_openlibs(_L);

        // Initialize the generated bindings
        LuaBindings.Initialize(_L);
    }

    [TestCleanup]
    public void TearDown()
    {
        // Clean up Lua state
        if (_L.Handle != 0)
        {
            lua_close(_L);
        }
    }

    private void AssertLuaOk(int result, string? context = null)
    {
        if (result != LUA_OK)
        {
            var error = lua_tostring(_L, -1) ?? "Unknown error";
            Assert.Fail($"Lua error{(context != null ? $" ({context})" : "")}: {error}");
        }
    }

    #region SampleClass Tests

    [TestMethod]
    public void SampleClass_Constructor_Default()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            return obj.id, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var id = lua_tointeger(_L, -2);

        Assert.AreEqual(0, id);
        Assert.AreEqual("", name);
    }

    [TestMethod]
    public void SampleClass_Constructor_WithIdAndName()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(42, 'TestName')
            return obj.id, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var id = lua_tointeger(_L, -2);

        Assert.AreEqual(42, id);
        Assert.AreEqual("TestName", name);
    }

    [TestMethod]
    public void SampleClass_Constructor_Full()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(10, 'FullTest', true, 3.14)
            return obj.id, obj.name, obj.isActive, obj.value
        ");
        AssertLuaOk(result);

        var value = lua_tonumber(_L, -1);
        var isActive = lua_toboolean(_L, -2);
        var name = lua_tostring(_L, -3);
        var id = lua_tointeger(_L, -4);

        Assert.AreEqual(10, id);
        Assert.AreEqual("FullTest", name);
        Assert.AreEqual(1, isActive);
        Assert.AreEqual(3.14, value, 0.01);
    }

    [TestMethod]
    public void SampleClass_PropertySet_ModifiesObject()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.id = 100
            obj.name = 'Modified'
            obj.isActive = true
            obj.value = 9.99
            return obj.id, obj.name, obj.isActive, obj.value
        ");
        AssertLuaOk(result);

        var value = lua_tonumber(_L, -1);
        var isActive = lua_toboolean(_L, -2);
        var name = lua_tostring(_L, -3);
        var id = lua_tointeger(_L, -4);

        Assert.AreEqual(100, id);
        Assert.AreEqual("Modified", name);
        Assert.AreEqual(1, isActive);
        Assert.AreEqual(9.99, value, 0.01);
    }

    [TestMethod]
    public void SampleClass_InstanceMethod_GetDoubleId()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(21, 'Test')
            return obj:getDoubleId()
        ");
        AssertLuaOk(result);

        var doubled = lua_tointeger(_L, -1);
        Assert.AreEqual(42, doubled);
    }

    [TestMethod]
    public void SampleClass_InstanceMethod_GetGreeting()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(1, 'World')
            return obj:getGreeting('Hello')
        ");
        AssertLuaOk(result);

        var greeting = lua_tostring(_L, -1);
        Assert.AreEqual("Hello World!", greeting);
    }

    [TestMethod]
    public void SampleClass_InstanceMethod_SetValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj:setValue(42.5)
            return obj.value
        ");
        AssertLuaOk(result);

        var value = lua_tonumber(_L, -1);
        Assert.AreEqual(42.5, value, 0.01);
    }

    [TestMethod]
    public void SampleClass_InstanceMethod_Calculate()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            local add = obj:calculate(3, 4, false)
            local mul = obj:calculate(3, 4, true)
            return add, mul
        ");
        AssertLuaOk(result);

        var mul = lua_tonumber(_L, -1);
        var add = lua_tonumber(_L, -2);

        Assert.AreEqual(7, add, 0.01);
        Assert.AreEqual(12, mul, 0.01);
    }

    [TestMethod]
    public void SampleClass_InstanceMethod_Clone()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(42, 'Original')
            local clone = obj:clone()
            clone.name = 'Cloned'
            return obj.name, clone.name, clone.id
        ");
        AssertLuaOk(result);

        var cloneId = lua_tointeger(_L, -1);
        var cloneName = lua_tostring(_L, -2);
        var origName = lua_tostring(_L, -3);

        Assert.AreEqual("Original", origName);
        Assert.AreEqual("Cloned", cloneName);
        Assert.AreEqual(42, cloneId);
    }

    [TestMethod]
    public void SampleClass_InstanceMethod_CustomName()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            return obj:customName()
        ");
        AssertLuaOk(result);

        var custom = lua_tostring(_L, -1);
        Assert.AreEqual("custom", custom);
    }

    [TestMethod]
    public void SampleClass_StaticMethod_Add()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.add(10, 20)
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        Assert.AreEqual(30, sum);
    }

    [TestMethod]
    public void SampleClass_StaticMethod_Concat()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.concat('Hello', ' World')
        ");
        AssertLuaOk(result);

        var concat = lua_tostring(_L, -1);
        Assert.AreEqual("Hello World", concat);
    }

    [TestMethod]
    public void SampleClass_StaticProperty_Counter()
    {
        // Reset the counter
        SampleClass.StaticCounter = 0;

        var result = luaL_dostring(_L, @"
            local before = SampleClass.staticCounter
            SampleClass.incrementCounter()
            SampleClass.incrementCounter()
            local after = SampleClass.staticCounter
            return before, after
        ");
        AssertLuaOk(result);

        var after = lua_tointeger(_L, -1);
        var before = lua_tointeger(_L, -2);

        Assert.AreEqual(0, before);
        Assert.AreEqual(2, after);
    }

    [TestMethod]
    public void SampleClass_StaticProperty_Name()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.staticName
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        Assert.AreEqual("SampleClass", name);
    }

    [TestMethod]
    public void SampleClass_Tostring()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(42, 'Test', true, 3.14)
            return tostring(obj)
        ");
        AssertLuaOk(result);

        var str = lua_tostring(_L, -1);
        Assert.IsTrue(str!.Contains("42"));
        Assert.IsTrue(str.Contains("Test"));
    }

    [TestMethod]
    public void SampleClass_InstanceProperty_PreciseValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.preciseValue = 3.141592653589793
            return obj.preciseValue
        ");
        AssertLuaOk(result);

        var preciseValue = lua_tonumber(_L, -1);
        Assert.AreEqual(3.141592653589793, preciseValue, 0.0000000000001);
    }

    [TestMethod]
    public void SampleClass_InstanceProperty_AllPropertiesRoundTrip()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.id = 123
            obj.name = 'RoundTrip'
            obj.isActive = true
            obj.value = 45.67
            obj.preciseValue = 89.1234567890123
            return obj.id, obj.name, obj.isActive, obj.value, obj.preciseValue
        ");
        AssertLuaOk(result);

        var preciseValue = lua_tonumber(_L, -1);
        var value = lua_tonumber(_L, -2);
        var isActive = lua_toboolean(_L, -3);
        var name = lua_tostring(_L, -4);
        var id = lua_tointeger(_L, -5);

        Assert.AreEqual(123, id);
        Assert.AreEqual("RoundTrip", name);
        Assert.AreEqual(1, isActive);
        Assert.AreEqual(45.67, value, 0.01);
        Assert.AreEqual(89.1234567890123, preciseValue, 0.0000000001);
    }

    [TestMethod]
    public void SampleClass_InstanceProperty_BooleanFalse()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(1, 'Test', true, 1.0)
            local before = obj.isActive
            obj.isActive = false
            local after = obj.isActive
            return before, after
        ");
        AssertLuaOk(result);

        var after = lua_toboolean(_L, -1);
        var before = lua_toboolean(_L, -2);

        Assert.AreEqual(1, before, "isActive should be true initially");
        Assert.AreEqual(0, after, "isActive should be false after setting");
    }

    [TestMethod]
    public void SampleClass_StaticProperty_CounterSet()
    {
        SampleClass.StaticCounter = 0;

        var result = luaL_dostring(_L, @"
            SampleClass.staticCounter = 50
            return SampleClass.staticCounter
        ");
        AssertLuaOk(result);

        var counter = lua_tointeger(_L, -1);
        Assert.AreEqual(50, counter);
        Assert.AreEqual(50, SampleClass.StaticCounter, "C# static should also be updated");
    }

    [TestMethod]
    public void SampleClass_StaticProperty_ReadOnly()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.staticName
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        Assert.AreEqual("SampleClass", name);
    }

    [TestMethod]
    public void SampleClass_PublicField_IntField()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.publicField = 999
            return obj.publicField
        ");
        AssertLuaOk(result);

        var field = lua_tointeger(_L, -1);
        Assert.AreEqual(999, field);
    }

    [TestMethod]
    public void SampleClass_PublicField_StringField()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.publicStringField = 'FieldValue'
            return obj.publicStringField
        ");
        AssertLuaOk(result);

        var field = lua_tostring(_L, -1);
        Assert.AreEqual("FieldValue", field);
    }

    #endregion

    #region Vec2 (SampleStruct) Tests

    [TestMethod]
    public void Vec2_Constructor_Default()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new()
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(0, x, 0.001);
        Assert.AreEqual(0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Constructor_WithValues()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3.5, 4.5)
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(3.5, x, 0.001);
        Assert.AreEqual(4.5, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Property_Length()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            return v.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        Assert.AreEqual(5.0, length, 0.001);
    }

    [TestMethod]
    public void Vec2_Property_LengthSquared()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            return v.lengthSquared
        ");
        AssertLuaOk(result);

        var lengthSq = lua_tonumber(_L, -1);
        Assert.AreEqual(25.0, lengthSq, 0.001);
    }

    [TestMethod]
    public void Vec2_FieldMutation_PersistsInStorage()
    {
        // This is the key struct mutation test!
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            local len1 = v.length  -- Should be 5

            -- Mutate the fields
            v.x = 6
            v.y = 8

            local len2 = v.length  -- Should be 10
            return len1, len2, v.x, v.y
        ");
        AssertLuaOk(result);

        var vy = lua_tonumber(_L, -1);
        var vx = lua_tonumber(_L, -2);
        var len2 = lua_tonumber(_L, -3);
        var len1 = lua_tonumber(_L, -4);

        Assert.AreEqual(5.0, len1, 0.001, "Initial length should be 5");
        Assert.AreEqual(10.0, len2, 0.001, "After mutation, length should be 10");
        Assert.AreEqual(6.0, vx, 0.001, "X should be mutated to 6");
        Assert.AreEqual(8.0, vy, 0.001, "Y should be mutated to 8");
    }

    [TestMethod]
    public void Vec2_MethodMutation_Set()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(1, 2)
            v:set(10, 20)
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(10.0, x, 0.001);
        Assert.AreEqual(20.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Operator_Add()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec2.new(1, 2)
            local v2 = Vec2.new(3, 4)
            local v3 = v1 + v2
            return v3.x, v3.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(4.0, x, 0.001);
        Assert.AreEqual(6.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Operator_Subtract()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec2.new(5, 7)
            local v2 = Vec2.new(2, 3)
            local v3 = v1 - v2
            return v3.x, v3.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(3.0, x, 0.001);
        Assert.AreEqual(4.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Operator_Multiply()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            local scaled = v * 2
            return scaled.x, scaled.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(6.0, x, 0.001);
        Assert.AreEqual(8.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Operator_Divide()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(6, 8)
            local divided = v / 2
            return divided.x, divided.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(3.0, x, 0.001);
        Assert.AreEqual(4.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Operator_UnaryNegation()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            local neg = -v
            return neg.x, neg.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(-3.0, x, 0.001);
        Assert.AreEqual(-4.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_Operator_Equality()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec2.new(3, 4)
            local v2 = Vec2.new(3, 4)
            local v3 = Vec2.new(1, 2)
            local eq1 = v1 == v2
            local eq2 = v1 == v3
            return eq1, eq2
        ");
        AssertLuaOk(result);

        var eq2 = lua_toboolean(_L, -1);
        var eq1 = lua_toboolean(_L, -2);

        Assert.AreEqual(1, eq1, "v1 == v2 should be true");
        Assert.AreEqual(0, eq2, "v1 == v3 should be false");
    }

    [TestMethod]
    public void Vec2_StaticMethod_Distance()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec2.new(0, 0)
            local b = Vec2.new(3, 4)
            return Vec2.distance(a, b)
        ");
        AssertLuaOk(result);

        var distance = lua_tonumber(_L, -1);
        Assert.AreEqual(5.0, distance, 0.001);
    }

    [TestMethod]
    public void Vec2_StaticMethod_Dot()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec2.new(1, 2)
            local b = Vec2.new(3, 4)
            return Vec2.dot(a, b)
        ");
        AssertLuaOk(result);

        var dot = lua_tonumber(_L, -1);
        Assert.AreEqual(11.0, dot, 0.001); // 1*3 + 2*4 = 11
    }

    [TestMethod]
    public void Vec2_StaticMethod_FromAngle()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.fromAngle(0)  -- 0 radians = pointing right
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(1.0, x, 0.001);
        Assert.AreEqual(0.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_InstanceMethod_Normalized()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            local n = v:normalized()
            return n.x, n.y, n.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(0.6, x, 0.001);
        Assert.AreEqual(0.8, y, 0.001);
        Assert.AreEqual(1.0, length, 0.001);
    }

    [TestMethod]
    public void Vec2_InstanceMethod_Scale()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            local s = v:scale(2)
            return s.x, s.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(6.0, x, 0.001);
        Assert.AreEqual(8.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_StaticProperty_Zero()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.zero
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(0.0, x, 0.001);
        Assert.AreEqual(0.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_StaticProperty_One()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.one
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(1.0, x, 0.001);
        Assert.AreEqual(1.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_StaticProperty_UnitX()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.unitX
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(1.0, x, 0.001);
        Assert.AreEqual(0.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_StaticProperty_UnitY()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.unitY
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(0.0, x, 0.001);
        Assert.AreEqual(1.0, y, 0.001);
    }

    [TestMethod]
    public void Vec2_InstanceProperty_LengthAfterMutation()
    {
        // Verify computed properties update after field mutation
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(0, 0)
            local len1 = v.length
            v.x = 3
            v.y = 4
            local len2 = v.length
            local lenSq = v.lengthSquared
            return len1, len2, lenSq
        ");
        AssertLuaOk(result);

        var lenSq = lua_tonumber(_L, -1);
        var len2 = lua_tonumber(_L, -2);
        var len1 = lua_tonumber(_L, -3);

        Assert.AreEqual(0.0, len1, 0.001);
        Assert.AreEqual(5.0, len2, 0.001);
        Assert.AreEqual(25.0, lenSq, 0.001);
    }

    [TestMethod]
    public void Vec2_InstanceProperty_FieldsIndependent()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(10, 20)
            v.x = 100
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(100.0, x, 0.001);
        Assert.AreEqual(20.0, y, 0.001, "Y should be unchanged");
    }

    [TestMethod]
    public void Vec2_Tostring()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3.5, 4.5)
            return tostring(v)
        ");
        AssertLuaOk(result);

        var str = lua_tostring(_L, -1);
        Assert.IsTrue(str!.Contains("3.5"));
        Assert.IsTrue(str.Contains("4.5"));
    }

    #endregion

    #region Vec3 (Vector3Struct) Tests

    [TestMethod]
    public void Vec3_Constructor_Default()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new()
            return v.x, v.y, v.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(0, x, 0.001);
        Assert.AreEqual(0, y, 0.001);
        Assert.AreEqual(0, z, 0.001);
    }

    [TestMethod]
    public void Vec3_Constructor_WithValues()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(1, 2, 3)
            return v.x, v.y, v.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(1, x, 0.001);
        Assert.AreEqual(2, y, 0.001);
        Assert.AreEqual(3, z, 0.001);
    }

    [TestMethod]
    public void Vec3_FieldMutation()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(1, 2, 3)
            v.x = 10
            v.y = 20
            v.z = 30
            return v.x, v.y, v.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(10, x, 0.001);
        Assert.AreEqual(20, y, 0.001);
        Assert.AreEqual(30, z, 0.001);
    }

    [TestMethod]
    public void Vec3_Property_Length()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(2, 3, 6)  -- length = sqrt(4+9+36) = 7
            return v.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        Assert.AreEqual(7.0, length, 0.001);
    }

    [TestMethod]
    public void Vec3_Operator_Add()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec3.new(1, 2, 3)
            local v2 = Vec3.new(4, 5, 6)
            local v3 = v1 + v2
            return v3.x, v3.y, v3.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(5, x, 0.001);
        Assert.AreEqual(7, y, 0.001);
        Assert.AreEqual(9, z, 0.001);
    }

    [TestMethod]
    public void Vec3_StaticMethod_Cross()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec3.new(1, 0, 0)
            local b = Vec3.new(0, 1, 0)
            local c = Vec3.cross(a, b)
            return c.x, c.y, c.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        // Cross product of X and Y axes should be Z axis
        Assert.AreEqual(0, x, 0.001);
        Assert.AreEqual(0, y, 0.001);
        Assert.AreEqual(1, z, 0.001);
    }

    [TestMethod]
    public void Vec3_StaticMethod_Dot()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec3.new(1, 2, 3)
            local b = Vec3.new(4, 5, 6)
            return Vec3.dot(a, b)
        ");
        AssertLuaOk(result);

        var dot = lua_tonumber(_L, -1);
        Assert.AreEqual(32, dot, 0.001); // 1*4 + 2*5 + 3*6 = 32
    }

    [TestMethod]
    public void Vec3_InstanceMethod_Normalized()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(2, 3, 6)  -- length = 7
            local n = v:normalized()
            return n.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        Assert.AreEqual(1.0, length, 0.001);
    }

    [TestMethod]
    public void Vec3_StaticProperty_Zero()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.zero
            return v.x, v.y, v.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(0, x, 0.001);
        Assert.AreEqual(0, y, 0.001);
        Assert.AreEqual(0, z, 0.001);
    }

    [TestMethod]
    public void Vec3_StaticProperty_One()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.one
            return v.x, v.y, v.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(1, x, 0.001);
        Assert.AreEqual(1, y, 0.001);
        Assert.AreEqual(1, z, 0.001);
    }

    [TestMethod]
    public void Vec3_InstanceProperty_LengthSquared()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(2, 3, 6)
            return v.length * v.length
        ");
        AssertLuaOk(result);

        var lengthSq = lua_tonumber(_L, -1);
        Assert.AreEqual(49.0, lengthSq, 0.001); // 4 + 9 + 36 = 49
    }

    [TestMethod]
    public void Vec3_InstanceProperty_LengthAfterMutation()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(0, 0, 0)
            local len1 = v.length
            v.x = 2
            v.y = 3
            v.z = 6
            local len2 = v.length
            return len1, len2
        ");
        AssertLuaOk(result);

        var len2 = lua_tonumber(_L, -1);
        var len1 = lua_tonumber(_L, -2);

        Assert.AreEqual(0.0, len1, 0.001);
        Assert.AreEqual(7.0, len2, 0.001);
    }

    [TestMethod]
    public void Vec3_Operator_Subtract()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec3.new(5, 7, 9)
            local v2 = Vec3.new(1, 2, 3)
            local v3 = v1 - v2
            return v3.x, v3.y, v3.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(4, x, 0.001);
        Assert.AreEqual(5, y, 0.001);
        Assert.AreEqual(6, z, 0.001);
    }

    [TestMethod]
    public void Vec3_Operator_Multiply()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(1, 2, 3)
            local scaled = v * 3
            return scaled.x, scaled.y, scaled.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(3, x, 0.001);
        Assert.AreEqual(6, y, 0.001);
        Assert.AreEqual(9, z, 0.001);
    }

    [TestMethod]
    public void Vec3_Operator_UnaryNegation()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(1, 2, 3)
            local neg = -v
            return neg.x, neg.y, neg.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(-1, x, 0.001);
        Assert.AreEqual(-2, y, 0.001);
        Assert.AreEqual(-3, z, 0.001);
    }

    [TestMethod]
    public void Vec3_Tostring()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(1, 2, 3)
            return tostring(v)
        ");
        AssertLuaOk(result);

        var str = lua_tostring(_L, -1);
        Assert.IsTrue(str!.Contains("1"));
        Assert.IsTrue(str.Contains("2"));
        Assert.IsTrue(str.Contains("3"));
    }

    #endregion

    #region Cross-Type Tests

    [TestMethod]
    public void Vec3_ToVec2_ReturnsVec2()
    {
        var result = luaL_dostring(_L, @"
            local v3 = Vec3.new(3, 4, 5)
            local v2 = v3:toVec2()
            return v2.x, v2.y, v2.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(3, x, 0.001);
        Assert.AreEqual(4, y, 0.001);
        Assert.AreEqual(5, length, 0.001);
    }

    [TestMethod]
    public void Vec3_FromVec2_CreatesVec3()
    {
        var result = luaL_dostring(_L, @"
            local v2 = Vec2.new(3, 4)
            local v3 = Vec3.fromVec2(v2, 5)
            return v3.x, v3.y, v3.z
        ");
        AssertLuaOk(result);

        var z = lua_tonumber(_L, -1);
        var y = lua_tonumber(_L, -2);
        var x = lua_tonumber(_L, -3);

        Assert.AreEqual(3, x, 0.001);
        Assert.AreEqual(4, y, 0.001);
        Assert.AreEqual(5, z, 0.001);
    }

    #endregion

    #region Complex Scenarios

    [TestMethod]
    public void Vec2_ChainedOperations()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec2.new(1, 2)
            local v2 = Vec2.new(3, 4)
            local result = (v1 + v2) * 2
            return result.x, result.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.AreEqual(8, x, 0.001);  // (1+3)*2 = 8
        Assert.AreEqual(12, y, 0.001); // (2+4)*2 = 12
    }

    [TestMethod]
    public void Vec2_MultipleInstances_Independent()
    {
        var result = luaL_dostring(_L, @"
            local v1 = Vec2.new(1, 2)
            local v2 = Vec2.new(3, 4)
            v1.x = 100
            return v1.x, v2.x
        ");
        AssertLuaOk(result);

        var v2x = lua_tonumber(_L, -1);
        var v1x = lua_tonumber(_L, -2);

        Assert.AreEqual(100, v1x, 0.001, "v1.x should be modified");
        Assert.AreEqual(3, v2x, 0.001, "v2.x should be unchanged");
    }

    [TestMethod]
    public void SampleClass_MultipleReferences_SameInstance()
    {
        // Unlike structs, class instances should share state
        var result = luaL_dostring(_L, @"
            local obj1 = SampleClass.new(1, 'Original')
            local obj2 = obj1  -- Same reference
            obj2.name = 'Modified'
            return obj1.name, obj2.name
        ");
        AssertLuaOk(result);

        var name2 = lua_tostring(_L, -1);
        var name1 = lua_tostring(_L, -2);

        Assert.AreEqual("Modified", name1, "obj1 should see modification");
        Assert.AreEqual("Modified", name2, "obj2 should be Modified");
    }

    [TestMethod]
    public void GarbageCollection_CleansUpStorage()
    {
        var initialCount = LuaBindings.ObjectCount;

        var result = luaL_dostring(_L, @"
            local v1 = Vec2.new(1, 2)
            local v2 = Vec2.new(3, 4)
            local v3 = Vec2.new(5, 6)
            v1 = nil
            v2 = nil
            v3 = nil
        ");
        AssertLuaOk(result);

        var afterCreate = LuaBindings.ObjectCount;
        Assert.IsTrue(afterCreate >= initialCount, "Objects should be created");

        // Force GC
        lua_gc(_L, LUA_GCCOLLECT, 0);
        lua_gc(_L, LUA_GCCOLLECT, 0);

        var afterGc = LuaBindings.ObjectCount;
        Assert.IsTrue(afterGc < afterCreate, "Objects should be cleaned up after GC");
    }

    #endregion

    #region Nullable Tests

    [TestMethod]
    public void SampleClass_NullableProperty_ReadNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            return obj.nullableInt
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        Assert.IsTrue(isNil == 1, "Nullable with no value should return nil");
    }

    [TestMethod]
    public void SampleClass_NullableProperty_ReadValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.nullableInt = 42
            return obj.nullableInt
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        var value = lua_tointeger(_L, -1);

        Assert.IsFalse(isNil == 1, "Should not be nil when value is set");
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void SampleClass_NullableProperty_WriteNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.nullableInt = 100
            obj.nullableInt = nil
            return obj.nullableInt
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        Assert.IsTrue(isNil == 1, "Setting to nil should clear the nullable");
    }

    [TestMethod]
    public void SampleClass_NullableFloat_ReadWriteCycle()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            -- Initially nil
            local v1 = obj.nullableFloat
            -- Set to value
            obj.nullableFloat = 3.14
            local v2 = obj.nullableFloat
            -- Set back to nil
            obj.nullableFloat = nil
            local v3 = obj.nullableFloat
            return v1, v2, v3
        ");
        AssertLuaOk(result);

        var v3IsNil = lua_isnil(_L, -1);
        var v2 = lua_tonumber(_L, -2);
        var v1IsNil = lua_isnil(_L, -3);

        Assert.IsTrue(v1IsNil == 1, "Initial nullable should be nil");
        Assert.AreEqual(3.14, v2, 0.001, "Value should be readable");
        Assert.IsTrue(v3IsNil == 1, "Setting to nil should clear value");
    }

    [TestMethod]
    public void SampleClass_NullableBool_TrueAndFalse()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.nullableBool = true
            local v1 = obj.nullableBool
            obj.nullableBool = false
            local v2 = obj.nullableBool
            obj.nullableBool = nil
            local v3 = obj.nullableBool
            return v1, v2, v3
        ");
        AssertLuaOk(result);

        var v3IsNil = lua_isnil(_L, -1);
        var v2 = lua_toboolean(_L, -2);
        var v1 = lua_toboolean(_L, -3);

        Assert.AreEqual(1, v1, "Should be true");
        Assert.AreEqual(0, v2, "Should be false");
        Assert.IsTrue(v3IsNil == 1, "Should be nil after clearing");
    }

    [TestMethod]
    public void SampleClass_NullableField_ReadNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            return obj.nullableLongField
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        Assert.IsTrue(isNil == 1, "Nullable field with no value should return nil");
    }

    [TestMethod]
    public void SampleClass_NullableField_WriteAndRead()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.nullableLongField = 999999
            local v1 = obj.nullableLongField
            obj.nullableLongField = nil
            local v2 = obj.nullableLongField
            return v1, v2
        ");
        AssertLuaOk(result);

        var v2IsNil = lua_isnil(_L, -1);
        var v1 = lua_tointeger(_L, -2);

        Assert.AreEqual(999999, v1, "Should read the value");
        Assert.IsTrue(v2IsNil == 1, "Should be nil after clearing");
    }

    [TestMethod]
    public void SampleClass_StaticNullableProperty_ReadNull()
    {
        var result = luaL_dostring(_L, @"
            SampleClass.staticNullableDouble = nil
            return SampleClass.staticNullableDouble
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        Assert.IsTrue(isNil == 1, "Static nullable should be nil");
    }

    [TestMethod]
    public void SampleClass_StaticNullableProperty_WriteAndRead()
    {
        var result = luaL_dostring(_L, @"
            SampleClass.staticNullableDouble = 2.71828
            local v1 = SampleClass.staticNullableDouble
            SampleClass.staticNullableDouble = nil
            local v2 = SampleClass.staticNullableDouble
            return v1, v2
        ");
        AssertLuaOk(result);

        var v2IsNil = lua_isnil(_L, -1);
        var v1 = lua_tonumber(_L, -2);

        Assert.AreEqual(2.71828, v1, 0.00001, "Should read the value");
        Assert.IsTrue(v2IsNil == 1, "Should be nil after clearing");
    }

    [TestMethod]
    public void SampleClass_MultipleNullables_IndependentState()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.nullableInt = 10
            obj.nullableFloat = 20.5
            obj.nullableBool = true

            -- Clear one
            obj.nullableInt = nil

            return obj.nullableInt, obj.nullableFloat, obj.nullableBool
        ");
        AssertLuaOk(result);

        var boolVal = lua_toboolean(_L, -1);
        var floatVal = lua_tonumber(_L, -2);
        var intIsNil = lua_isnil(_L, -3);

        Assert.IsTrue(intIsNil == 1, "Int should be nil");
        Assert.AreEqual(20.5, floatVal, 0.001, "Float should retain value");
        Assert.AreEqual(1, boolVal, "Bool should retain value");
    }

    #endregion

    #region Nullable Parameter Tests

    [TestMethod]
    public void SampleClass_ConstructorWithNullableParams_BothNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(nil, nil)
            return obj.id, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var id = lua_tointeger(_L, -2);

        Assert.AreEqual(0, id, "Null id should default to 0");
        Assert.AreEqual("", name, "Null name should default to empty");
    }

    [TestMethod]
    public void SampleClass_ConstructorWithNullableParams_WithValues()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(99, 'NullableTest')
            return obj.id, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var id = lua_tointeger(_L, -2);

        Assert.AreEqual(99, id);
        Assert.AreEqual("NullableTest", name);
    }

    [TestMethod]
    public void SampleClass_ConstructorWithNullableParams_MixedNulls()
    {
        var result = luaL_dostring(_L, @"
            local obj1 = SampleClass.new(50, nil)
            local obj2 = SampleClass.new(nil, 'OnlyName')
            return obj1.id, obj1.name, obj2.id, obj2.name
        ");
        AssertLuaOk(result);

        var name2 = lua_tostring(_L, -1);
        var id2 = lua_tointeger(_L, -2);
        var name1 = lua_tostring(_L, -3);
        var id1 = lua_tointeger(_L, -4);

        Assert.AreEqual(50, id1);
        Assert.AreEqual("", name1);
        Assert.AreEqual(0, id2);
        Assert.AreEqual("OnlyName", name2);
    }

    [TestMethod]
    public void SampleClass_AddNullable_BothNull()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.addNullable(nil, nil)
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        Assert.AreEqual(0, sum, "nil + nil should be 0");
    }

    [TestMethod]
    public void SampleClass_AddNullable_OneNull()
    {
        var result = luaL_dostring(_L, @"
            local r1 = SampleClass.addNullable(5, nil)
            local r2 = SampleClass.addNullable(nil, 10)
            return r1, r2
        ");
        AssertLuaOk(result);

        var r2 = lua_tointeger(_L, -1);
        var r1 = lua_tointeger(_L, -2);

        Assert.AreEqual(5, r1, "5 + nil should be 5");
        Assert.AreEqual(10, r2, "nil + 10 should be 10");
    }

    [TestMethod]
    public void SampleClass_AddNullable_BothValues()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.addNullable(7, 13)
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        Assert.AreEqual(20, sum);
    }

    [TestMethod]
    public void SampleClass_GetNullableValue_ReturnsNull()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.getNullableValue(false, 42)
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        Assert.IsTrue(isNil == 1, "Should return nil when hasValue is false");
    }

    [TestMethod]
    public void SampleClass_GetNullableValue_ReturnsValue()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.getNullableValue(true, 42)
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(42, value, "Should return 42 when hasValue is true");
    }

    [TestMethod]
    public void SampleClass_SetNullableValue_WithNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.value = 100
            obj:setNullableValue(nil)
            return obj.value
        ");
        AssertLuaOk(result);

        var value = lua_tonumber(_L, -1);
        Assert.AreEqual(0, value, 0.001, "Setting nil should reset to 0");
    }

    [TestMethod]
    public void SampleClass_SetNullableValue_WithValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj:setNullableValue(3.14)
            return obj.value
        ");
        AssertLuaOk(result);

        var value = lua_tonumber(_L, -1);
        Assert.AreEqual(3.14, value, 0.001, "Should set the value");
    }

    [TestMethod]
    public void SampleClass_MultiplyByNullable_WithNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.id = 10
            return obj:multiplyByNullable(nil)
        ");
        AssertLuaOk(result);

        var isNil = lua_isnil(_L, -1);
        Assert.IsTrue(isNil == 1, "Should return nil when multiplier is nil");
    }

    [TestMethod]
    public void SampleClass_MultiplyByNullable_WithValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.id = 10
            return obj:multiplyByNullable(5)
        ");
        AssertLuaOk(result);

        var value = lua_tointeger(_L, -1);
        Assert.AreEqual(50, value, "10 * 5 should be 50");
    }

    [TestMethod]
    public void SampleClass_FormatWithOptional_BothNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.name = 'Test'
            return obj:formatWithOptional(nil, nil)
        ");
        AssertLuaOk(result);

        var formatted = lua_tostring(_L, -1);
        Assert.AreEqual("Test", formatted, "Should return just the name");
    }

    [TestMethod]
    public void SampleClass_FormatWithOptional_WithValues()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.name = 'Test'
            return obj:formatWithOptional('[', ']')
        ");
        AssertLuaOk(result);

        var formatted = lua_tostring(_L, -1);
        Assert.AreEqual("[Test]", formatted, "Should format with brackets");
    }

    [TestMethod]
    public void SampleClass_FormatWithOptional_OnlyPrefix()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.name = 'Test'
            return obj:formatWithOptional('>', nil)
        ");
        AssertLuaOk(result);

        var formatted = lua_tostring(_L, -1);
        Assert.AreEqual(">Test", formatted, "Should format with prefix only");
    }

    #endregion

    #region Referenced Type Discovery Tests

    [TestMethod]
    public void ReferencedType_Constructor_WorksFromLua()
    {
        var result = luaL_dostring(_L, @"
            local obj = ReferencedType.new(42, 'Test')
            return obj.value, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var value = lua_tointeger(_L, -2);

        Assert.AreEqual(42, value);
        Assert.AreEqual("Test", name);
    }

    [TestMethod]
    public void ReferencedType_GetDescription_ReturnsFormatted()
    {
        var result = luaL_dostring(_L, @"
            local obj = ReferencedType.new(10, 'Sample')
            return obj:getDescription()
        ");
        AssertLuaOk(result);

        var desc = lua_tostring(_L, -1);
        Assert.AreEqual("ReferencedType: Sample = 10", desc);
    }

    [TestMethod]
    public void TypeWithReferences_CreateReferenced_ReturnsReferencedType()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithReferences.new()
            local ref = obj:createReferenced(99, 'Created')
            return ref.value, ref.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var value = lua_tointeger(_L, -2);

        Assert.AreEqual(99, value);
        Assert.AreEqual("Created", name);
    }

    [TestMethod]
    public void TypeWithReferences_ReferencedProperty_CanSetAndGet()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithReferences.new()
            local ref = ReferencedType.new(50, 'Property')
            obj.referenced = ref
            return obj.referenced.value, obj.referenced.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var value = lua_tointeger(_L, -2);

        Assert.AreEqual(50, value);
        Assert.AreEqual("Property", name);
    }

    [TestMethod]
    public void TypeWithReferences_CreateNumberList_ReturnsGenericList()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithReferences.new()
            local list = obj:createNumberList(3)
            -- Note: We can't directly access List<int> members from Lua yet,
            -- but we can verify the method returns successfully
            return list ~= nil
        ");
        AssertLuaOk(result);

        var notNil = lua_toboolean(_L, -1);
        Assert.AreEqual(1, notNil, "Should return a non-nil list");
    }

    [TestMethod]
    public void TypeWithReferences_ConstructorWithReferencedType_Works()
    {
        var result = luaL_dostring(_L, @"
            local ref = ReferencedType.new(100, 'Constructor')
            local obj = TypeWithReferences.new(ref)
            return obj.referenced.value, obj.referenced.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var value = lua_tointeger(_L, -2);

        Assert.AreEqual(100, value);
        Assert.AreEqual("Constructor", name);
    }

    #endregion

    #region TypeWithArrays Tests

    [TestMethod]
    public void TypeWithArrays_Constructor_CreatesEmptyObject()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithArrays.new()
            return obj ~= nil
        ");
        AssertLuaOk(result);

        var notNil = lua_toboolean(_L, -1);
        Assert.AreEqual(1, notNil);
    }

    [TestMethod]
    public void TypeWithArrays_GetLength_ReturnsZeroForNullArray()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithArrays.new()
            return obj:getLength()
        ");
        AssertLuaOk(result);

        var length = lua_tointeger(_L, -1);
        Assert.AreEqual(0, length);
    }

    [TestMethod]
    public void TypeWithArrays_SumNumbers_ReturnsZeroForNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithArrays.new()
            return obj:sumNumbers()
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        Assert.AreEqual(0, sum);
    }

    [TestMethod]
    public void TypeWithArrays_ConcatenateNames_ReturnsEmptyForNull()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithArrays.new()
            return obj:concatenateNames()
        ");
        AssertLuaOk(result);

        var result2 = lua_tostring(_L, -1);
        Assert.AreEqual("", result2);
    }

    [TestMethod]
    public void TypeWithArrays_CreateSequence_ReturnsArray()
    {
        var result = luaL_dostring(_L, @"
            local arr = TypeWithArrays.createSequence(5)
            return arr ~= nil
        ");
        AssertLuaOk(result);

        var notNil = lua_toboolean(_L, -1);
        Assert.AreEqual(1, notNil, "Should return a non-nil array");
    }

    [TestMethod]
    public void TypeWithArrays_ArrayIndexing_ReadElement()
    {
        var result = luaL_dostring(_L, @"
            local arr = TypeWithArrays.createSequence(5)
            return arr[1], arr[2], arr[5]
        ");
        AssertLuaOk(result);

        var val3 = lua_tointeger(_L, -1);
        var val2 = lua_tointeger(_L, -2);
        var val1 = lua_tointeger(_L, -3);

        Assert.AreEqual(1, val1);
        Assert.AreEqual(2, val2);
        Assert.AreEqual(5, val3);
    }

    [TestMethod]
    public void TypeWithArrays_ArrayIndexing_WriteElement()
    {
        var result = luaL_dostring(_L, @"
            local arr = TypeWithArrays.createSequence(5)
            arr[1] = 100
            arr[3] = 300
            return arr[1], arr[2], arr[3]
        ");
        AssertLuaOk(result);

        var val3 = lua_tointeger(_L, -1);
        var val2 = lua_tointeger(_L, -2);
        var val1 = lua_tointeger(_L, -3);

        Assert.AreEqual(100, val1);
        Assert.AreEqual(2, val2);
        Assert.AreEqual(300, val3);
    }

    [TestMethod]
    public void TypeWithArrays_ArrayMutation_StaysInSync()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithArrays.new()
            local arr = TypeWithArrays.createSequence(3)
            obj.numbers = arr

            -- Modify through array reference
            arr[2] = 999

            -- Should see change through object property
            return obj.numbers[2]
        ");
        AssertLuaOk(result);

        var val = lua_tointeger(_L, -1);
        Assert.AreEqual(999, val, "Array mutations should be visible through object property");
    }

    [TestMethod]
    public void TypeWithArrays_PropertyArray_CanBeIndexed()
    {
        var result = luaL_dostring(_L, @"
            local arr = TypeWithArrays.createSequence(5)
            local obj = TypeWithArrays.new(arr)
            return obj.numbers[1], obj.numbers[5]
        ");
        AssertLuaOk(result);

        var val2 = lua_tointeger(_L, -1);
        var val1 = lua_tointeger(_L, -2);

        Assert.AreEqual(1, val1);
        Assert.AreEqual(5, val2);
    }

    [TestMethod]
    public void TypeWithArrays_PropertyArray_CanBeModified()
    {
        var result = luaL_dostring(_L, @"
            local arr = TypeWithArrays.createSequence(5)
            local obj = TypeWithArrays.new(arr)
            obj.numbers[3] = 777
            return obj.numbers[3], obj:sumNumbers()
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        var val = lua_tointeger(_L, -2);

        Assert.AreEqual(777, val);
        // Sum should be 1 + 2 + 777 + 4 + 5 = 789
        Assert.AreEqual(789, sum);
    }

    #endregion

    #region TypeWithMultiDimArray Tests

    [TestMethod]
    public void TypeWithMultiDimArray_Constructor_CreatesEmptyObject()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithMultiDimArray.new()
            return obj ~= nil
        ");
        AssertLuaOk(result);

        var notNil = lua_toboolean(_L, -1);
        Assert.AreEqual(1, notNil);
    }

    [TestMethod]
    public void TypeWithMultiDimArray_CreateIdentityMatrix_ReturnsMatrix()
    {
        var result = luaL_dostring(_L, @"
            local matrix = TypeWithMultiDimArray.createIdentityMatrix(3)
            return matrix ~= nil
        ");
        AssertLuaOk(result);

        var notNil = lua_toboolean(_L, -1);
        Assert.AreEqual(1, notNil);
    }

    [TestMethod]
    public void TypeWithMultiDimArray_MatrixIndexing_TableSyntax_Read()
    {
        var result = luaL_dostring(_L, @"
            local matrix = TypeWithMultiDimArray.createIdentityMatrix(3)
            return matrix[{1,1}], matrix[{1,2}], matrix[{2,2}]
        ");
        AssertLuaOk(result);

        var val3 = lua_tointeger(_L, -1);
        var val2 = lua_tointeger(_L, -2);
        var val1 = lua_tointeger(_L, -3);

        Assert.AreEqual(1, val1, "matrix[0,0] should be 1");
        Assert.AreEqual(0, val2, "matrix[0,1] should be 0");
        Assert.AreEqual(1, val3, "matrix[1,1] should be 1");
    }

    [TestMethod]
    public void TypeWithMultiDimArray_MatrixIndexing_TableSyntax_Write()
    {
        var result = luaL_dostring(_L, @"
            local matrix = TypeWithMultiDimArray.createIdentityMatrix(3)
            matrix[{1,2}] = 42
            matrix[{2,1}] = 99
            return matrix[{1,2}], matrix[{2,1}]
        ");
        AssertLuaOk(result);

        var val2 = lua_tointeger(_L, -1);
        var val1 = lua_tointeger(_L, -2);

        Assert.AreEqual(42, val1, "matrix[0,1] should be 42");
        Assert.AreEqual(99, val2, "matrix[1,0] should be 99");
    }

    [TestMethod]
    public void TypeWithMultiDimArray_PropertyMatrix_CanBeIndexed()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithMultiDimArray.new()
            obj:initializeMatrix(2, 3)
            return obj.matrix[{1,1}], obj.matrix[{1,3}], obj.matrix[{2,2}]
        ");
        AssertLuaOk(result);

        var val3 = lua_tointeger(_L, -1);
        var val2 = lua_tointeger(_L, -2);
        var val1 = lua_tointeger(_L, -3);

        // InitializeMatrix sets matrix[i,j] = i * cols + j
        // So matrix[0,0] = 0, matrix[0,2] = 2, matrix[1,1] = 4
        Assert.AreEqual(0, val1);
        Assert.AreEqual(2, val2);
        Assert.AreEqual(4, val3);
    }

    [TestMethod]
    public void TypeWithMultiDimArray_PropertyMatrix_Modification_StaysInSync()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithMultiDimArray.new()
            obj:initializeMatrix(2, 2)
            obj.matrix[{2,2}] = 500
            return obj:sumAll()
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        // Original: [0,1], [2,3] = 6 total
        // Modified: [0,1], [2,500] = 503 total
        Assert.AreEqual(503, sum);
    }

    #endregion

    #region TypeWithIndexers Tests

    [TestMethod]
    public void TypeWithIndexers_Constructor_CreatesEmptyObject()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithIndexers.new()
            return obj ~= nil
        ");
        AssertLuaOk(result);

        var notNil = lua_toboolean(_L, -1);
        Assert.AreEqual(1, notNil);
    }

    [TestMethod]
    public void TypeWithIndexers_IntIndexer_ReadAndWrite()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithIndexers.new()
            obj[1] = 10
            obj[5] = 50
            return obj[1], obj[5]
        ");
        AssertLuaOk(result);

        var val2 = lua_tointeger(_L, -1);
        var val1 = lua_tointeger(_L, -2);

        Assert.AreEqual(10, val1);
        Assert.AreEqual(50, val2);
    }

    [TestMethod]
    public void TypeWithIndexers_IntIndexer_StaysInSync()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithIndexers.new()
            obj[3] = 123
            return obj:getNumberAt(2)
        ");
        AssertLuaOk(result);

        var val = lua_tointeger(_L, -1);
        Assert.AreEqual(123, val, "Indexer and method should access same data");
    }

    [TestMethod]
    public void TypeWithIndexers_MultiParamIndexer_TableSyntax_ReadAndWrite()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithIndexers.new()
            obj[{1,2}] = 'hello'
            obj[{3,4}] = 'world'
            return obj[{1,2}], obj[{3,4}]
        ");
        AssertLuaOk(result);

        var val2 = lua_tostring(_L, -1);
        var val1 = lua_tostring(_L, -2);

        Assert.AreEqual("hello", val1);
        Assert.AreEqual("world", val2);
    }

    [TestMethod]
    public void TypeWithIndexers_MultiParamIndexer_StaysInSync()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithIndexers.new()
            obj[{5,6}] = 'test'
            return obj:getGridValue(5, 6)
        ");
        AssertLuaOk(result);

        var val = lua_tostring(_L, -1);
        Assert.AreEqual("test", val, "Indexer and method should access same data");
    }

    [TestMethod]
    public void TypeWithIndexers_MixedIndexerAccess()
    {
        var result = luaL_dostring(_L, @"
            local obj = TypeWithIndexers.new()
            -- Int indexer (1D)
            obj[1] = 111
            -- Grid indexer (2D table)
            obj[{10,20}] = 'grid_value'

            return obj[1], obj[{10,20}], obj:getNumberAt(0), obj:getGridValue(10, 20)
        ");
        AssertLuaOk(result);

        var gridVal2 = lua_tostring(_L, -1);
        var intVal2 = lua_tointeger(_L, -2);
        var gridVal1 = lua_tostring(_L, -3);
        var intVal1 = lua_tointeger(_L, -4);

        Assert.AreEqual(111, intVal1);
        Assert.AreEqual("grid_value", gridVal1);
        Assert.AreEqual(111, intVal2);
        Assert.AreEqual("grid_value", gridVal2);
    }

    #endregion
}
