using LuaNET.LuaJIT;
using static LuaNET.LuaJIT.Lua;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using NFMWorld.LuaSourceGenerator.Test.TestBindings;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Runtime integration tests that spawn a real LuaJIT runtime and verify
/// that generated bindings for SampleTypes work correctly from the Lua side.
/// </summary>
[TestFixture]
public class LuaRuntimeTests
{
    private lua_State _L;

    [SetUp]
    public void Setup()
    {
        // Reset bindings state for test isolation
        LuaBindings.Reset();

        // Create a new Lua state for each test
        _L = luaL_newstate();
        luaL_openlibs(_L);

        // Initialize the generated bindings
        LuaBindings.Initialize(_L);
    }

    [TearDown]
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

    [Test]
    public void SampleClass_Constructor_Default()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            return obj.id, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var id = lua_tointeger(_L, -2);

        Assert.That(id, Is.EqualTo(0));
        Assert.That(name, Is.EqualTo(""));
    }

    [Test]
    public void SampleClass_Constructor_WithIdAndName()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(42, 'TestName')
            return obj.id, obj.name
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        var id = lua_tointeger(_L, -2);

        Assert.That(id, Is.EqualTo(42));
        Assert.That(name, Is.EqualTo("TestName"));
    }

    [Test]
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

        Assert.That(id, Is.EqualTo(10));
        Assert.That(name, Is.EqualTo("FullTest"));
        Assert.That(isActive, Is.EqualTo(1));
        Assert.That(value, Is.EqualTo(3.14).Within(0.01));
    }

    [Test]
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

        Assert.That(id, Is.EqualTo(100));
        Assert.That(name, Is.EqualTo("Modified"));
        Assert.That(isActive, Is.EqualTo(1));
        Assert.That(value, Is.EqualTo(9.99).Within(0.01));
    }

    [Test]
    public void SampleClass_InstanceMethod_GetDoubleId()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(21, 'Test')
            return obj:getDoubleId()
        ");
        AssertLuaOk(result);

        var doubled = lua_tointeger(_L, -1);
        Assert.That(doubled, Is.EqualTo(42));
    }

    [Test]
    public void SampleClass_InstanceMethod_GetGreeting()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(1, 'World')
            return obj:getGreeting('Hello')
        ");
        AssertLuaOk(result);

        var greeting = lua_tostring(_L, -1);
        Assert.That(greeting, Is.EqualTo("Hello World!"));
    }

    [Test]
    public void SampleClass_InstanceMethod_SetValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj:setValue(42.5)
            return obj.value
        ");
        AssertLuaOk(result);

        var value = lua_tonumber(_L, -1);
        Assert.That(value, Is.EqualTo(42.5).Within(0.01));
    }

    [Test]
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

        Assert.That(add, Is.EqualTo(7).Within(0.01));
        Assert.That(mul, Is.EqualTo(12).Within(0.01));
    }

    [Test]
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

        Assert.That(origName, Is.EqualTo("Original"));
        Assert.That(cloneName, Is.EqualTo("Cloned"));
        Assert.That(cloneId, Is.EqualTo(42));
    }

    [Test]
    public void SampleClass_InstanceMethod_CustomName()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            return obj:customName()
        ");
        AssertLuaOk(result);

        var custom = lua_tostring(_L, -1);
        Assert.That(custom, Is.EqualTo("custom"));
    }

    [Test]
    public void SampleClass_StaticMethod_Add()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.add(10, 20)
        ");
        AssertLuaOk(result);

        var sum = lua_tointeger(_L, -1);
        Assert.That(sum, Is.EqualTo(30));
    }

    [Test]
    public void SampleClass_StaticMethod_Concat()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.concat('Hello', ' World')
        ");
        AssertLuaOk(result);

        var concat = lua_tostring(_L, -1);
        Assert.That(concat, Is.EqualTo("Hello World"));
    }

    [Test]
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

        Assert.That(before, Is.EqualTo(0));
        Assert.That(after, Is.EqualTo(2));
    }

    [Test]
    public void SampleClass_StaticProperty_Name()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.staticName
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        Assert.That(name, Is.EqualTo("SampleClass"));
    }

    [Test]
    public void SampleClass_Tostring()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new(42, 'Test', true, 3.14)
            return tostring(obj)
        ");
        AssertLuaOk(result);

        var str = lua_tostring(_L, -1);
        Assert.That(str, Does.Contain("42"));
        Assert.That(str, Does.Contain("Test"));
    }

    [Test]
    public void SampleClass_InstanceProperty_PreciseValue()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.preciseValue = 3.141592653589793
            return obj.preciseValue
        ");
        AssertLuaOk(result);

        var preciseValue = lua_tonumber(_L, -1);
        Assert.That(preciseValue, Is.EqualTo(3.141592653589793).Within(0.0000000000001));
    }

    [Test]
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

        Assert.That(id, Is.EqualTo(123));
        Assert.That(name, Is.EqualTo("RoundTrip"));
        Assert.That(isActive, Is.EqualTo(1));
        Assert.That(value, Is.EqualTo(45.67).Within(0.01));
        Assert.That(preciseValue, Is.EqualTo(89.1234567890123).Within(0.0000000001));
    }

    [Test]
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

        Assert.That(before, Is.EqualTo(1), "isActive should be true initially");
        Assert.That(after, Is.EqualTo(0), "isActive should be false after setting");
    }

    [Test]
    public void SampleClass_StaticProperty_CounterSet()
    {
        SampleClass.StaticCounter = 0;

        var result = luaL_dostring(_L, @"
            SampleClass.staticCounter = 50
            return SampleClass.staticCounter
        ");
        AssertLuaOk(result);

        var counter = lua_tointeger(_L, -1);
        Assert.That(counter, Is.EqualTo(50));
        Assert.That(SampleClass.StaticCounter, Is.EqualTo(50), "C# static should also be updated");
    }

    [Test]
    public void SampleClass_StaticProperty_ReadOnly()
    {
        var result = luaL_dostring(_L, @"
            return SampleClass.staticName
        ");
        AssertLuaOk(result);

        var name = lua_tostring(_L, -1);
        Assert.That(name, Is.EqualTo("SampleClass"));
    }

    [Test]
    public void SampleClass_PublicField_IntField()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.publicField = 999
            return obj.publicField
        ");
        AssertLuaOk(result);

        var field = lua_tointeger(_L, -1);
        Assert.That(field, Is.EqualTo(999));
    }

    [Test]
    public void SampleClass_PublicField_StringField()
    {
        var result = luaL_dostring(_L, @"
            local obj = SampleClass.new()
            obj.publicStringField = 'FieldValue'
            return obj.publicStringField
        ");
        AssertLuaOk(result);

        var field = lua_tostring(_L, -1);
        Assert.That(field, Is.EqualTo("FieldValue"));
    }

    #endregion

    #region Vec2 (SampleStruct) Tests

    [Test]
    public void Vec2_Constructor_Default()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new()
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(0).Within(0.001));
        Assert.That(y, Is.EqualTo(0).Within(0.001));
    }

    [Test]
    public void Vec2_Constructor_WithValues()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3.5, 4.5)
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(3.5).Within(0.001));
        Assert.That(y, Is.EqualTo(4.5).Within(0.001));
    }

    [Test]
    public void Vec2_Property_Length()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            return v.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        Assert.That(length, Is.EqualTo(5.0).Within(0.001));
    }

    [Test]
    public void Vec2_Property_LengthSquared()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3, 4)
            return v.lengthSquared
        ");
        AssertLuaOk(result);

        var lengthSq = lua_tonumber(_L, -1);
        Assert.That(lengthSq, Is.EqualTo(25.0).Within(0.001));
    }

    [Test]
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

        Assert.That(len1, Is.EqualTo(5.0).Within(0.001), "Initial length should be 5");
        Assert.That(len2, Is.EqualTo(10.0).Within(0.001), "After mutation, length should be 10");
        Assert.That(vx, Is.EqualTo(6.0).Within(0.001), "X should be mutated to 6");
        Assert.That(vy, Is.EqualTo(8.0).Within(0.001), "Y should be mutated to 8");
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(10.0).Within(0.001));
        Assert.That(y, Is.EqualTo(20.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(4.0).Within(0.001));
        Assert.That(y, Is.EqualTo(6.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(3.0).Within(0.001));
        Assert.That(y, Is.EqualTo(4.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(6.0).Within(0.001));
        Assert.That(y, Is.EqualTo(8.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(3.0).Within(0.001));
        Assert.That(y, Is.EqualTo(4.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(-3.0).Within(0.001));
        Assert.That(y, Is.EqualTo(-4.0).Within(0.001));
    }

    [Test]
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

        Assert.That(eq1, Is.EqualTo(1), "v1 == v2 should be true");
        Assert.That(eq2, Is.EqualTo(0), "v1 == v3 should be false");
    }

    [Test]
    public void Vec2_StaticMethod_Distance()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec2.new(0, 0)
            local b = Vec2.new(3, 4)
            return Vec2.distance(a, b)
        ");
        AssertLuaOk(result);

        var distance = lua_tonumber(_L, -1);
        Assert.That(distance, Is.EqualTo(5.0).Within(0.001));
    }

    [Test]
    public void Vec2_StaticMethod_Dot()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec2.new(1, 2)
            local b = Vec2.new(3, 4)
            return Vec2.dot(a, b)
        ");
        AssertLuaOk(result);

        var dot = lua_tonumber(_L, -1);
        Assert.That(dot, Is.EqualTo(11.0).Within(0.001)); // 1*3 + 2*4 = 11
    }

    [Test]
    public void Vec2_StaticMethod_FromAngle()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.fromAngle(0)  -- 0 radians = pointing right
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(1.0).Within(0.001));
        Assert.That(y, Is.EqualTo(0.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(0.6).Within(0.001));
        Assert.That(y, Is.EqualTo(0.8).Within(0.001));
        Assert.That(length, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(6.0).Within(0.001));
        Assert.That(y, Is.EqualTo(8.0).Within(0.001));
    }

    [Test]
    public void Vec2_StaticProperty_Zero()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.zero
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(0.0).Within(0.001));
        Assert.That(y, Is.EqualTo(0.0).Within(0.001));
    }

    [Test]
    public void Vec2_StaticProperty_One()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.one
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(1.0).Within(0.001));
        Assert.That(y, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void Vec2_StaticProperty_UnitX()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.unitX
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(1.0).Within(0.001));
        Assert.That(y, Is.EqualTo(0.0).Within(0.001));
    }

    [Test]
    public void Vec2_StaticProperty_UnitY()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.unitY
            return v.x, v.y
        ");
        AssertLuaOk(result);

        var y = lua_tonumber(_L, -1);
        var x = lua_tonumber(_L, -2);

        Assert.That(x, Is.EqualTo(0.0).Within(0.001));
        Assert.That(y, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
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

        Assert.That(len1, Is.EqualTo(0.0).Within(0.001));
        Assert.That(len2, Is.EqualTo(5.0).Within(0.001));
        Assert.That(lenSq, Is.EqualTo(25.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(100.0).Within(0.001));
        Assert.That(y, Is.EqualTo(20.0).Within(0.001), "Y should be unchanged");
    }

    [Test]
    public void Vec2_Tostring()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec2.new(3.5, 4.5)
            return tostring(v)
        ");
        AssertLuaOk(result);

        var str = lua_tostring(_L, -1);
        Assert.That(str, Does.Contain("3.5"));
        Assert.That(str, Does.Contain("4.5"));
    }

    #endregion

    #region Vec3 (Vector3Struct) Tests

    [Test]
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

        Assert.That(x, Is.EqualTo(0).Within(0.001));
        Assert.That(y, Is.EqualTo(0).Within(0.001));
        Assert.That(z, Is.EqualTo(0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(1).Within(0.001));
        Assert.That(y, Is.EqualTo(2).Within(0.001));
        Assert.That(z, Is.EqualTo(3).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(10).Within(0.001));
        Assert.That(y, Is.EqualTo(20).Within(0.001));
        Assert.That(z, Is.EqualTo(30).Within(0.001));
    }

    [Test]
    public void Vec3_Property_Length()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(2, 3, 6)  -- length = sqrt(4+9+36) = 7
            return v.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        Assert.That(length, Is.EqualTo(7.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(5).Within(0.001));
        Assert.That(y, Is.EqualTo(7).Within(0.001));
        Assert.That(z, Is.EqualTo(9).Within(0.001));
    }

    [Test]
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
        Assert.That(x, Is.EqualTo(0).Within(0.001));
        Assert.That(y, Is.EqualTo(0).Within(0.001));
        Assert.That(z, Is.EqualTo(1).Within(0.001));
    }

    [Test]
    public void Vec3_StaticMethod_Dot()
    {
        var result = luaL_dostring(_L, @"
            local a = Vec3.new(1, 2, 3)
            local b = Vec3.new(4, 5, 6)
            return Vec3.dot(a, b)
        ");
        AssertLuaOk(result);

        var dot = lua_tonumber(_L, -1);
        Assert.That(dot, Is.EqualTo(32).Within(0.001)); // 1*4 + 2*5 + 3*6 = 32
    }

    [Test]
    public void Vec3_InstanceMethod_Normalized()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(2, 3, 6)  -- length = 7
            local n = v:normalized()
            return n.length
        ");
        AssertLuaOk(result);

        var length = lua_tonumber(_L, -1);
        Assert.That(length, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(0).Within(0.001));
        Assert.That(y, Is.EqualTo(0).Within(0.001));
        Assert.That(z, Is.EqualTo(0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(1).Within(0.001));
        Assert.That(y, Is.EqualTo(1).Within(0.001));
        Assert.That(z, Is.EqualTo(1).Within(0.001));
    }

    [Test]
    public void Vec3_InstanceProperty_LengthSquared()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(2, 3, 6)
            return v.length * v.length
        ");
        AssertLuaOk(result);

        var lengthSq = lua_tonumber(_L, -1);
        Assert.That(lengthSq, Is.EqualTo(49.0).Within(0.001)); // 4 + 9 + 36 = 49
    }

    [Test]
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

        Assert.That(len1, Is.EqualTo(0.0).Within(0.001));
        Assert.That(len2, Is.EqualTo(7.0).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(4).Within(0.001));
        Assert.That(y, Is.EqualTo(5).Within(0.001));
        Assert.That(z, Is.EqualTo(6).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(3).Within(0.001));
        Assert.That(y, Is.EqualTo(6).Within(0.001));
        Assert.That(z, Is.EqualTo(9).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(-1).Within(0.001));
        Assert.That(y, Is.EqualTo(-2).Within(0.001));
        Assert.That(z, Is.EqualTo(-3).Within(0.001));
    }

    [Test]
    public void Vec3_Tostring()
    {
        var result = luaL_dostring(_L, @"
            local v = Vec3.new(1, 2, 3)
            return tostring(v)
        ");
        AssertLuaOk(result);

        var str = lua_tostring(_L, -1);
        Assert.That(str, Does.Contain("1"));
        Assert.That(str, Does.Contain("2"));
        Assert.That(str, Does.Contain("3"));
    }

    #endregion

    #region Cross-Type Tests

    [Test]
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

        Assert.That(x, Is.EqualTo(3).Within(0.001));
        Assert.That(y, Is.EqualTo(4).Within(0.001));
        Assert.That(length, Is.EqualTo(5).Within(0.001));
    }

    [Test]
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

        Assert.That(x, Is.EqualTo(3).Within(0.001));
        Assert.That(y, Is.EqualTo(4).Within(0.001));
        Assert.That(z, Is.EqualTo(5).Within(0.001));
    }

    #endregion

    #region Complex Scenarios

    [Test]
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

        Assert.That(x, Is.EqualTo(8).Within(0.001));  // (1+3)*2 = 8
        Assert.That(y, Is.EqualTo(12).Within(0.001)); // (2+4)*2 = 12
    }

    [Test]
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

        Assert.That(v1x, Is.EqualTo(100).Within(0.001), "v1.x should be modified");
        Assert.That(v2x, Is.EqualTo(3).Within(0.001), "v2.x should be unchanged");
    }

    [Test]
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

        Assert.That(name1, Is.EqualTo("Modified"), "obj1 should see modification");
        Assert.That(name2, Is.EqualTo("Modified"), "obj2 should be Modified");
    }

    [Test]
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
        Assert.That(afterCreate, Is.GreaterThanOrEqualTo(initialCount), "Objects should be created");

        // Force GC
        lua_gc(_L, LUA_GCCOLLECT, 0);
        lua_gc(_L, LUA_GCCOLLECT, 0);

        var afterGc = LuaBindings.ObjectCount;
        Assert.That(afterGc, Is.LessThan(afterCreate), "Objects should be cleaned up after GC");
    }

    #endregion
}
