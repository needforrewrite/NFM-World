using Lua;
using Lua.Runtime;
using Lua.Standard;
using nfm_world_library.Lua;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Ported runtime tests — migrated from LuaJIT to Lua-CSharp.
/// Tests that generated bindings work correctly from the Lua side.
/// </summary>
[TestClass]
public class LuaRuntimePortedTests
{
    private LuaState _state = null!;

    [TestInitialize]
    public void Setup()
    {
        _state = LuaState.Create();
        _state.OpenBasicLibrary();
        LuaVisibleTypeRegistry.RegisterAll(_state);
    }

    [TestCleanup]
    public void TearDown()
    {
        _state.Dispose();
    }

    // ===================================================================
    // SampleClass tests
    // ===================================================================

    [TestMethod]
    public async Task SampleClass_Constructor_Default()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            return obj.id, obj.name
        ");
        Assert.AreEqual(0, results[0].Read<int>());
        Assert.AreEqual("", results[1].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_Constructor_WithIdAndName()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str(42, 'TestName')
            return obj.id, obj.name
        ");
        Assert.AreEqual(42, results[0].Read<int>());
        Assert.AreEqual("TestName", results[1].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_Constructor_Full()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str_bool_flt(10, 'FullTest', true, 3.14)
            return obj.id, obj.name, obj.isActive, obj.value
        ");
        Assert.AreEqual(10, results[0].Read<int>());
        Assert.AreEqual("FullTest", results[1].Read<string>());
        Assert.IsTrue(results[2].Read<bool>());
        Assert.AreEqual(3.14, results[3].Read<double>(), 0.01);
    }

    [TestMethod]
    public async Task SampleClass_PropertySet_ModifiesObject()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            obj.id = 100
            obj.name = 'Modified'
            obj.isActive = true
            obj.value = 9.99
            return obj.id, obj.name, obj.isActive, obj.value
        ");
        Assert.AreEqual(100, results[0].Read<int>());
        Assert.AreEqual("Modified", results[1].Read<string>());
        Assert.IsTrue(results[2].Read<bool>());
        Assert.AreEqual(9.99, results[3].Read<double>(), 0.01);
    }

    [TestMethod]
    public async Task SampleClass_InstanceMethod_GetDoubleId()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str(21, 'Test')
            return obj:getDoubleId()
        ");
        Assert.AreEqual(42, results[0].Read<int>());
    }

    [TestMethod]
    public async Task SampleClass_InstanceMethod_GetGreeting()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str(1, 'World')
            return obj:getGreeting('Hello')
        ");
        Assert.AreEqual("Hello World!", results[0].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_InstanceMethod_SetValue()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            obj:setValue(42.5)
            return obj.value
        ");
        Assert.AreEqual(42.5, results[0].Read<double>(), 0.01);
    }

    [TestMethod]
    public async Task SampleClass_InstanceMethod_Calculate()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            local add = obj:calculate(3, 4, false)
            local mul = obj:calculate(3, 4, true)
            return add, mul
        ");
        Assert.AreEqual(7, results[0].Read<double>(), 0.01);
        Assert.AreEqual(12, results[1].Read<double>(), 0.01);
    }

    [TestMethod]
    public async Task SampleClass_InstanceMethod_Clone()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str(42, 'Original')
            local clone = obj:clone()
            clone.name = 'Cloned'
            return obj.name, clone.name, clone.id
        ");
        Assert.AreEqual("Original", results[0].Read<string>());
        Assert.AreEqual("Cloned", results[1].Read<string>());
        Assert.AreEqual(42, results[2].Read<int>());
    }

    [TestMethod]
    public async Task SampleClass_InstanceMethod_CustomName()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            return obj:customName()
        ");
        Assert.AreEqual("custom", results[0].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_StaticMethod_Add()
    {
        var results = await _state.DoStringAsync(@"return SampleClass.add(10, 20)");
        Assert.AreEqual(30, results[0].Read<int>());
    }

    [TestMethod]
    public async Task SampleClass_StaticMethod_Concat()
    {
        var results = await _state.DoStringAsync(@"return SampleClass.concat('Hello', ' World')");
        Assert.AreEqual("Hello World", results[0].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_StaticProperty_Counter()
    {
        // Reset the counter via C#
        SampleClass.StaticCounter = 0;

        var results = await _state.DoStringAsync(@"
            local before = SampleClass.staticCounter
            SampleClass.incrementCounter()
            SampleClass.incrementCounter()
            local after = SampleClass.staticCounter
            return before, after
        ");
        Assert.AreEqual(0, results[0].Read<int>());
        Assert.AreEqual(2, results[1].Read<int>());
    }

    [TestMethod]
    public async Task SampleClass_StaticProperty_Name()
    {
        var results = await _state.DoStringAsync(@"return SampleClass.staticName");
        Assert.AreEqual("SampleClass", results[0].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_Tostring()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str_bool_flt(42, 'Test', true, 3.14)
            return tostring(obj)
        ");
        var str = results[0].Read<string>();
        Assert.IsTrue(str.Contains("42"));
        Assert.IsTrue(str.Contains("Test"));
    }

    [TestMethod]
    public async Task SampleClass_InstanceProperty_PreciseValue()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            obj.preciseValue = 3.141592653589793
            return obj.preciseValue
        ");
        Assert.AreEqual(3.141592653589793, results[0].Read<double>(), 0.0001);
    }

    [TestMethod]
    public async Task SampleClass_PublicField_ReadWrite()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            obj.publicField = 12345
            return obj.publicField
        ");
        Assert.AreEqual(12345, results[0].Read<int>());
    }

    [TestMethod]
    public async Task SampleClass_PublicStringField()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new()
            obj.publicStringField = 'hello field'
            return obj.publicStringField
        ");
        Assert.AreEqual("hello field", results[0].Read<string>());
    }

    [TestMethod]
    public async Task SampleClass_BooleanFalse_RoundTrip()
    {
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str_bool_flt(0, '', false, 0)
            return obj.isActive
        ");
        Assert.IsFalse(results[0].Read<bool>());
    }

    [TestMethod]
    public async Task SampleClass_ReadOnlyProperty()
    {
        // Id is read-only via the generated binding
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_int_str(99, 'ReadOnly')
            return obj.id
        ");
        Assert.AreEqual(99, results[0].Read<int>());
    }

    // ===================================================================
    // Nullable tests
    // ===================================================================

    [TestMethod]
    public async Task Nullable_Int_ReadNull()
    {
        var obj = new SampleClass();
        _state.Environment["obj"] = (LuaValue)obj;
        var results = await _state.DoStringAsync("return obj.nullableInt");
        Assert.AreEqual(LuaValueType.Nil, results[0].Type);
    }

    [TestMethod]
    public async Task Nullable_Int_SetAndRead()
    {
        var obj = new SampleClass();
        _state.Environment["obj"] = (LuaValue)obj;
        await _state.DoStringAsync("obj.nullableInt = 42");
        Assert.AreEqual(42, obj.NullableInt);
    }

    [TestMethod]
    public async Task Nullable_Int_SetToNil()
    {
        var obj = new SampleClass { NullableInt = 42 };
        _state.Environment["obj"] = (LuaValue)obj;
        await _state.DoStringAsync("obj.nullableInt = nil");
        Assert.IsNull(obj.NullableInt);
    }

    [TestMethod]
    public async Task Nullable_Bool_ThreeState()
    {
        var obj = new SampleClass();
        _state.Environment["obj"] = (LuaValue)obj;

        // Initially null
        var r1 = await _state.DoStringAsync("return obj.nullableBool");
        Assert.AreEqual(LuaValueType.Nil, r1[0].Type);

        // Set to true
        await _state.DoStringAsync("obj.nullableBool = true");
        Assert.IsTrue(obj.NullableBool);

        // Set to false
        await _state.DoStringAsync("obj.nullableBool = false");
        Assert.IsFalse(obj.NullableBool);

        // Set back to nil
        await _state.DoStringAsync("obj.nullableBool = nil");
        Assert.IsNull(obj.NullableBool);
    }

    [TestMethod]
    public async Task Nullable_Float_RoundTrip()
    {
        var obj = new SampleClass();
        _state.Environment["obj"] = (LuaValue)obj;
        await _state.DoStringAsync("obj.nullableFloat = 3.14");
        Assert.AreEqual(3.14f, obj.NullableFloat!.Value, 0.01f);
    }

    [TestMethod]
    public async Task Nullable_Long_Field()
    {
        var obj = new SampleClass { NullableLongField = 1234567890123L };
        _state.Environment["obj"] = (LuaValue)obj;
        var results = await _state.DoStringAsync("return obj.nullableLongField");
        Assert.AreEqual(1234567890123.0, results[0].Read<double>(), 1.0);
    }
}


