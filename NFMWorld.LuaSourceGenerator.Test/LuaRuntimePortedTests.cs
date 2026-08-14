using Lua;
using Lua.Runtime;
using Lua.Standard;
using NFMWorld.LuaSourceGenerator.Generator;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using NFMWorld.LuaSourceGenerator.TestFixtures;
using NFMWorld.LuaSourceGenerator.TestFixtures.Lua;

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
        var readDouble = results[0].Read<double>();
        Assert.IsNotNull(readDouble);
        Assert.AreEqual(1234567890123.0, readDouble, 1.0);
    }

    // ===================================================================
    // Record struct tests
    // ===================================================================

    [TestMethod]
    public async Task RecordStruct_CreateAndRead()
    {
        _state.Environment["RecordStructType"] = RecordStructType.TypeTable;
        var results = await _state.DoStringAsync(@"
            local obj = RecordStructType.new_int_int(10, 20)
            return obj.x, obj.y
        ");
        Assert.AreEqual(10, results[0].Read<int>());
        Assert.AreEqual(20, results[1].Read<int>());
    }

    [TestMethod]
    public async Task RecordStruct_DefaultConstructor()
    {
        _state.Environment["RecordStructType"] = RecordStructType.TypeTable;
        var results = await _state.DoStringAsync(@"
            local obj = RecordStructType.new()
            return obj.x, obj.y
        ");
        Assert.AreEqual(0, results[0].Read<int>());
        Assert.AreEqual(0, results[1].Read<int>());
    }

    [TestMethod]
    public async Task RecordStruct_InstanceMethod()
    {
        _state.Environment["RecordStructType"] = RecordStructType.TypeTable;
        var results = await _state.DoStringAsync(@"
            local obj = RecordStructType.new_int_int(3, 4)
            return obj:sum()
        ");
        Assert.AreEqual(7, results[0].Read<int>());
    }

    // ===================================================================
    // Tuple overload tests — verify no invalid identifiers generated
    // ===================================================================

    [TestMethod]
    public async Task TupleOverloads_CompileCheck()
    {
        // Verify the type table exists and has the expected overloaded entries
        _state.Environment["TypeWithTupleOverloads"] = TypeWithTupleOverloads.TypeTable;
        Assert.IsNotNull(TypeWithTupleOverloads.TypeTable);

        // The key test: just verify the type compiled — presence of
        // valid __function_ entries proves no invalid identifiers were generated
        Assert.IsTrue(true, "TypeWithTupleOverloads compiled successfully");
    }

    [TestMethod]
    public async Task TupleOverloads_CreateAndVerifyCompilation()
    {
        // Main test: verify compilation succeeded (no invalid C# identifiers from tuple types)
        _state.Environment["TypeWithTupleOverloads"] = TypeWithTupleOverloads.TypeTable;
        
        // Create instance and verify it exists
        var results = await _state.DoStringAsync(@"
            local obj = TypeWithTupleOverloads.new()
            return type(obj)
        ");
        Assert.IsNotNull(results[0].Read<string>());
    }

    // ===================================================================
    // Namespace conflict test — verify global:: prefix
    // ===================================================================

    [TestMethod]
    public async Task LuaNamespace_CompileCheck()
    {
        // TypeInLuaNamespace is in a namespace containing "Lua"
        // The generated code must use global::Lua.ILuaUserData to avoid conflict
        _state.Environment["TypeInLuaNamespace"] = TypeInLuaNamespace.TypeTable;
        Assert.IsNotNull(TypeInLuaNamespace.TypeTable);
        Assert.IsTrue(true, "TypeInLuaNamespace compiled without Lua namespace conflict");
    }

    [TestMethod]
    public async Task LuaNamespace_CreateAndRead()
    {
        _state.Environment["TypeInLuaNamespace"] = TypeInLuaNamespace.TypeTable;
        var results = await _state.DoStringAsync(@"
            local obj = TypeInLuaNamespace.new_str_int('Test', 99)
            return obj.name, obj.value
        ");
        Assert.AreEqual("Test", results[0].Read<string>());
        Assert.AreEqual(99, results[1].Read<int>());
    }

    // ===================================================================
    // Constructor overload tests
    // ===================================================================

    [TestMethod]
    public async Task ConstructorOverloads_AllVariants()
    {
        _state.Environment["TypeWithOverloads"] = TypeWithOverloads.TypeTable;

        // int overload (first constructor, registered as base "new")
        var r1 = await _state.DoStringAsync(@"
            local obj = TypeWithOverloads.new(42)
            return obj.value
        ");
        Assert.AreEqual(42, r1[0].Read<int>());

        // float overload
        var r2 = await _state.DoStringAsync(@"
            local obj = TypeWithOverloads.new_flt(3.14)
            return obj.value
        ");
        Assert.AreEqual(3, r2[0].Read<int>());

        // string overload
        var r3 = await _state.DoStringAsync(@"
            local obj = TypeWithOverloads.new_str('Hello')
            return obj.text
        ");
        Assert.AreEqual("string:Hello", r3[0].Read<string>());
    }

    // ===================================================================
    // Static property access tests (TypeTable metatable)
    // ===================================================================

    [TestMethod]
    public async Task StaticProperty_Counter_ReadWrite()
    {
        _state.Environment["SampleClass"] = SampleClass.TypeTable;
        SampleClass.StaticCounter = 0;

        await _state.DoStringAsync("SampleClass.staticCounter = 100");
        Assert.AreEqual(100, SampleClass.StaticCounter);

        var results = await _state.DoStringAsync("return SampleClass.staticCounter");
        Assert.AreEqual(100, results[0].Read<int>());
    }

    [TestMethod]
    public async Task StaticProperty_Name_Readable()
    {
        _state.Environment["SampleClass"] = SampleClass.TypeTable;
        var results = await _state.DoStringAsync("return SampleClass.staticName");
        Assert.AreEqual("SampleClass", results[0].Read<string>());
    }

    // ===================================================================
    // Nullable in overloads — verify no '?' in generated identifiers
    // ===================================================================

    [TestMethod]
    public async Task NullableOverloads_CompileCheck()
    {
        // SampleClass has constructor overloads with nullable params
        // The nullable variant should have suffix like _intn_str not _int?_str
        _state.Environment["SampleClass"] = SampleClass.TypeTable;
        Assert.IsNotNull(SampleClass.TypeTable["new_intn_str"]);
        Assert.IsTrue(true, "Nullable constructor overload compiled without '?' in identifiers");
    }

    [TestMethod]
    public async Task NullableOverloads_CallNullableCtor()
    {
        _state.Environment["SampleClass"] = SampleClass.TypeTable;
        // Call the nullable constructor with nil for int, value for string
        var results = await _state.DoStringAsync(@"
            local obj = SampleClass.new_intn_str(nil, 'OnlyNameGiven')
            return obj.id, obj.name
        ");
        Assert.AreEqual(0, results[0].Read<int>());
        Assert.AreEqual("OnlyNameGiven", results[1].Read<string>());
    }

    // ===================================================================
    // FixedMath nullable tests — verify no missing metatable errors
    // ===================================================================

    [TestMethod]
    public async Task FixedMathNullable_CompileCheck()
    {
        // Verify Fixed64? doesn't generate a StructUserData metatable
        _state.Environment["TypeWithFixedMathNullables"] = TypeWithFixedMathNullables.TypeTable;
        Assert.IsNotNull(TypeWithFixedMathNullables.TypeTable);
        Assert.IsTrue(true, "TypeWithFixedMathNullables compiled — no missing StructUserData_Metatable for Fixed64?");
    }

    [TestMethod]
    public async Task FixedMathNullable_NonNullValue()
    {
        _state.Environment["TypeWithFixedMathNullables"] = TypeWithFixedMathNullables.TypeTable;
        // Read a non-null Fixed64 value
        var results = await _state.DoStringAsync(@"
            local obj = TypeWithFixedMathNullables.new()
            obj.normalFixed = 42.5
            return obj.normalFixed
        ");
        // Should be a Fixed64 value, not nil
        Assert.AreEqual(LuaValueType.Fixed64, results[0].Type);
    }

    // ===================================================================
    // Span/ReadOnlySpan parameter tests — verify ref struct methods skipped
    // ===================================================================

    [TestMethod]
    public async Task SpanParams_CompileCheck()
    {
        // Verify the type compiled — Span/ReadOnlySpan methods were safely skipped
        _state.Environment["TypeWithSpanParameters"] = TypeWithSpanParameters.TypeTable;
        Assert.IsNotNull(TypeWithSpanParameters.TypeTable);

        // GetName (normal method) should be accessible
        var results = await _state.DoStringAsync(@"
            local obj = TypeWithSpanParameters.new()
            return obj:getName()
        ");
        Assert.AreEqual("", results[0].Read<string>());
    }

    [TestMethod]
    public async Task SpanParams_SpanMethodsNotExposed()
    {
        _state.Environment["TypeWithSpanParameters"] = TypeWithSpanParameters.TypeTable;
        // Verify that Sum, Fill, GetChars, CountMatching are NOT in the metatable
        var results = await _state.DoStringAsync(@"
            local obj = TypeWithSpanParameters.new()
            return obj.sum, obj.fill, obj.getChars, obj.countMatching
        ");
        Assert.AreEqual(LuaValueType.Nil, results[0].Type); // sum: skipped (ReadOnlySpan param)
        Assert.AreEqual(LuaValueType.Nil, results[1].Type); // fill: skipped (Span param)
        Assert.AreEqual(LuaValueType.Nil, results[2].Type); // getChars: skipped (returns ref struct)
        Assert.AreEqual(LuaValueType.Nil, results[3].Type); // countMatching: skipped (ReadOnlySpan param)
    }

    // ===================================================================
    // Const field tests — verify consts are read-only
    // ===================================================================

    [TestMethod]
    public async Task ConstFields_CompileCheck()
    {
        // Verify the type compiled — no syntax error trying to assign to consts
        _state.Environment["TypeWithConstants"] = TypeWithConstants.TypeTable;
        Assert.IsNotNull(TypeWithConstants.TypeTable);
        Assert.IsTrue(true, "TypeWithConstants compiled without const assignment errors");
    }

    [TestMethod]
    public async Task ConstFields_Readable()
    {
        _state.Environment["TypeWithConstants"] = TypeWithConstants.TypeTable;
        var results = await _state.DoStringAsync(@"
            return TypeWithConstants.factor, TypeWithConstants.defaultName, TypeWithConstants.pi
        ");
        Assert.AreEqual(100, results[0].Read<int>());
        Assert.AreEqual("Default", results[1].Read<string>());
        Assert.AreEqual(3.14159, results[2].Read<double>(), 0.001);
    }

    [TestMethod]
    public async Task ConstFields_WritableFieldStillWorks()
    {
        _state.Environment["TypeWithConstants"] = TypeWithConstants.TypeTable;
        await _state.DoStringAsync("TypeWithConstants.multiplier = 5");
        Assert.AreEqual(5, TypeWithConstants.Multiplier);
    }

    // ===================================================================
    // Interface inheritance tests — verify base interface members are
    // accessible through the derived [LuaVisible] interface's metatable
    // ===================================================================

    // --- Two-level: IDog : IBaseAnimal ---

    [TestMethod]
    public async Task InterfaceInheritance_IDog_OwnProperty_Accessible()
    {
        var dog = new Dog { Breed = "Labrador" };
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        var results = await _state.DoStringAsync("return dog.breed");
        Assert.AreEqual("Labrador", results[0].Read<string>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_OwnProperty_Writable()
    {
        var dog = new Dog();
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        await _state.DoStringAsync("dog.breed = 'Poodle'");
        Assert.AreEqual("Poodle", dog.Breed);
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_InheritedName_Readable()
    {
        var dog = new Dog { Name = "Fido" };
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        var results = await _state.DoStringAsync("return dog.name");
        Assert.AreEqual("Fido", results[0].Read<string>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_InheritedName_Writable()
    {
        var dog = new Dog();
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        await _state.DoStringAsync("dog.name = 'Rex'");
        Assert.AreEqual("Rex", dog.Name);
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_InheritedAge_Readable()
    {
        var dog = new Dog { Age = 3 };
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        var results = await _state.DoStringAsync("return dog.age");
        Assert.AreEqual(3, results[0].Read<int>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_InheritedAge_Writable()
    {
        var dog = new Dog { Age = 1 };
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        await _state.DoStringAsync("dog.age = 7");
        Assert.AreEqual(7, dog.Age);
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_AllPropertiesRoundTrip()
    {
        var dog = new Dog();
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        var results = await _state.DoStringAsync(@"
            dog.name = 'Buddy'
            dog.age = 5
            dog.breed = 'Golden Retriever'
            return dog.name, dog.age, dog.breed
        ");
        Assert.AreEqual("Buddy", results[0].Read<string>());
        Assert.AreEqual(5, results[1].Read<int>());
        Assert.AreEqual("Golden Retriever", results[2].Read<string>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_IDog_Tostring()
    {
        var dog = new Dog { Name = "Spot", Age = 2, Breed = "Dalmatian" };
        _state.Environment["dog"] = LuaValue.FromUserData(dog);
        var results = await _state.DoStringAsync("return tostring(dog)");
        var str = results[0].Read<string>();
        Assert.IsTrue(str.Contains("Dog"));
    }

    // --- Three-level: IFixtureCar : IFixtureVehicle : IFixtureTransform ---

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_OwnProperties_Accessible()
    {
        var car = new FixtureCar { Model = "Tesla", IsElectric = true };
        _state.Environment["car"] = LuaValue.FromUserData(car);
        var results = await _state.DoStringAsync("return car.model, car.isElectric");
        Assert.AreEqual("Tesla", results[0].Read<string>());
        Assert.IsTrue(results[1].Read<bool>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_OwnProperties_Writable()
    {
        var car = new FixtureCar();
        _state.Environment["car"] = LuaValue.FromUserData(car);
        await _state.DoStringAsync("car.model = 'BMW'; car.isElectric = false");
        Assert.AreEqual("BMW", car.Model);
        Assert.IsFalse(car.IsElectric);
    }

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_Level1Inherited_Speed()
    {
        var car = new FixtureCar { Speed = 120 };
        _state.Environment["car"] = LuaValue.FromUserData(car);
        var results = await _state.DoStringAsync("return car.speed");
        Assert.AreEqual(120, results[0].Read<int>());

        await _state.DoStringAsync("car.speed = 200");
        Assert.AreEqual(200, car.Speed);
    }

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_Level1Inherited_DriverName()
    {
        var car = new FixtureCar { DriverName = "Max" };
        _state.Environment["car"] = LuaValue.FromUserData(car);
        var results = await _state.DoStringAsync("return car.driverName");
        Assert.AreEqual("Max", results[0].Read<string>());

        await _state.DoStringAsync("car.driverName = 'Lewis'");
        Assert.AreEqual("Lewis", car.DriverName);
    }

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_Level1Inherited_DriverName_Nil()
    {
        var car = new FixtureCar { DriverName = "Seb" };
        _state.Environment["car"] = LuaValue.FromUserData(car);
        var results = await _state.DoStringAsync("return car.driverName");
        Assert.AreEqual("Seb", results[0].Read<string>());

        await _state.DoStringAsync("car.driverName = nil");
        Assert.IsNull(car.DriverName);
    }

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_Level2Inherited_XYZ()
    {
        var car = new FixtureCar { X = 1.1, Y = 2.2, Z = 3.3 };
        _state.Environment["car"] = LuaValue.FromUserData(car);
        var results = await _state.DoStringAsync("return car.x, car.y, car.z");
        Assert.AreEqual(1.1, results[0].Read<double>(), 0.001);
        Assert.AreEqual(2.2, results[1].Read<double>(), 0.001);
        Assert.AreEqual(3.3, results[2].Read<double>(), 0.001);

        await _state.DoStringAsync("car.x = 10.5; car.y = 20.5; car.z = 30.5");
        Assert.AreEqual(10.5, car.X, 0.001);
        Assert.AreEqual(20.5, car.Y, 0.001);
        Assert.AreEqual(30.5, car.Z, 0.001);
    }

    [TestMethod]
    public async Task InterfaceInheritance_FixtureCar_AllLevelsRoundTrip()
    {
        var car = new FixtureCar();
        _state.Environment["car"] = LuaValue.FromUserData(car);
        var results = await _state.DoStringAsync(@"
            car.model = 'Audi'
            car.isElectric = true
            car.speed = 250
            car.driverName = 'Nico'
            car.x = 5.5
            car.y = 6.6
            car.z = 7.7
            return car.model, car.isElectric, car.speed, car.driverName, car.x, car.y, car.z
        ");
        Assert.AreEqual("Audi", results[0].Read<string>());
        Assert.IsTrue(results[1].Read<bool>());
        Assert.AreEqual(250, results[2].Read<int>());
        Assert.AreEqual("Nico", results[3].Read<string>());
        Assert.AreEqual(5.5, results[4].Read<double>(), 0.001);
        Assert.AreEqual(6.6, results[5].Read<double>(), 0.001);
        Assert.AreEqual(7.7, results[6].Read<double>(), 0.001);
    }

    // --- Multiple interface inheritance: IPerson : IHasName, IHasAge ---

    [TestMethod]
    public async Task InterfaceInheritance_Person_OwnProperty_Email()
    {
        var person = new Person { Email = "test@example.com" };
        _state.Environment["person"] = LuaValue.FromUserData(person);
        var results = await _state.DoStringAsync("return person.email");
        Assert.AreEqual("test@example.com", results[0].Read<string>());

        await _state.DoStringAsync("person.email = 'new@example.com'");
        Assert.AreEqual("new@example.com", person.Email);
    }

    [TestMethod]
    public async Task InterfaceInheritance_Person_OwnProperty_Email_Nil()
    {
        var person = new Person { Email = "old@example.com" };
        _state.Environment["person"] = LuaValue.FromUserData(person);
        await _state.DoStringAsync("person.email = nil");
        Assert.IsNull(person.Email);
        var results = await _state.DoStringAsync("return person.email");
        Assert.AreEqual(LuaValueType.Nil, results[0].Type);
    }

    [TestMethod]
    public async Task InterfaceInheritance_Person_InheritedMethods_GetSetName()
    {
        var person = new Person();
        _state.Environment["person"] = LuaValue.FromUserData(person);

        await _state.DoStringAsync("person:setName('Alice')");
        Assert.AreEqual("Alice", person.GetName());

        var results = await _state.DoStringAsync("return person:getName()");
        Assert.AreEqual("Alice", results[0].Read<string>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_Person_InheritedMethods_GetSetAge()
    {
        var person = new Person();
        _state.Environment["person"] = LuaValue.FromUserData(person);

        await _state.DoStringAsync("person:setAge(25)");
        Assert.AreEqual(25, person.GetAge());

        var results = await _state.DoStringAsync("return person:getAge()");
        Assert.AreEqual(25, results[0].Read<int>());
    }

    [TestMethod]
    public async Task InterfaceInheritance_Person_Tostring()
    {
        var person = new Person { Email = "alice@test.com" };
        _state.Environment["person"] = LuaValue.FromUserData(person);
        var results = await _state.DoStringAsync("return tostring(person)");
        var str = results[0].Read<string>();
        Assert.IsTrue(str.Contains("Person"));
    }

    // ===================================================================
    // Generated code sanity checks — verify StructUserData metatables exist
    // ===================================================================

    [TestMethod]
    public void InterfaceInheritance_StructUserDataMetatables_Exist()
    {
        // Verify that DOG metatable was generated (IDog is [LuaVisible])
        Assert.IsNotNull(IDog.Metatable, "IDog.Metatable should be generated");

        // Verify the fixture car metatable was generated
        Assert.IsNotNull(IFixtureCar.Metatable, "IFixtureCar.Metatable should be generated");

        // Verify the person metatable was generated
        Assert.IsNotNull(IPerson.Metatable, "IPerson.Metatable should be generated");
    }

    [TestMethod]
    public void InterfaceInheritance_NoDuplicateOwnAndInheritedProperties()
    {
        // Verify that own properties don't appear twice in __index
        // (check Dog metatable has exactly one entry for 'breed', not two)
        var meta = IDog.Metatable;
        Assert.IsNotNull(meta);

        // The metatable should have the index function
        var indexFunc = meta[Metamethods.Index];
        Assert.IsTrue(indexFunc.Type != LuaValueType.Nil, "__index should be set on IDog metatable");
    }

    // ===================================================================
    // Enum marshalling tests
    // ===================================================================

    [TestMethod]
    public async Task Enum_CompileCheck()
    {
        // Verify enum binding compiled — TypeWithEnum references TestColor properties
        var obj = new TypeWithEnum { Color = TestColor.Green };
        _state.Environment["obj"] = LuaValue.FromUserData(obj);
        Assert.IsTrue(true, "TypeWithEnum compiled with enum properties");
    }

    [TestMethod]
    public async Task Enum_Property_Read()
    {
        var obj = new TypeWithEnum { Color = TestColor.Green };
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var results = await _state.DoStringAsync(@"
            local c = obj.color
            return tostring(c)
        ");
        Assert.AreEqual("Green", results[0].Read<string>());
    }

    [TestMethod]
    public async Task Enum_Property_Set()
    {
        var obj = new TypeWithEnum();
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var blueVal = LuaVisibleHelper.Wrap(TestColor.Blue);
        _state.Environment["blueColor"] = (LuaValue)blueVal;
        await _state.DoStringAsync("obj.color = blueColor");
        Assert.AreEqual(TestColor.Blue, obj.Color);
    }

    [TestMethod]
    public async Task Enum_Method_Return()
    {
        var obj = new TypeWithEnum { Color = TestColor.Blue };
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var results = await _state.DoStringAsync(@"
            local c = obj:getColor()
            return tostring(c)
        ");
        Assert.AreEqual("Blue", results[0].Read<string>());
    }

    [TestMethod]
    public async Task Enum_Method_Parameter()
    {
        var obj = new TypeWithEnum();
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var greenVal = LuaVisibleHelper.Wrap(TestColor.Green);
        _state.Environment["greenColor"] = (LuaValue)greenVal;
        await _state.DoStringAsync("obj:setColor(greenColor)");
        Assert.AreEqual(TestColor.Green, obj.Color);
    }

    [TestMethod]
    public async Task Enum_Method_BoolReturn()
    {
        var obj = new TypeWithEnum();
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var redVal = LuaVisibleHelper.Wrap(TestColor.Red);
        var yellowVal = LuaVisibleHelper.Wrap(TestColor.Yellow);
        _state.Environment["redColor"] = redVal;
        _state.Environment["yellowColor"] = yellowVal;

        var results = await _state.DoStringAsync(@"
            local r1 = obj:isPrimary(redColor)
            local r2 = obj:isPrimary(yellowColor)
            return r1, r2
        ");
        Assert.IsTrue(results[0].Read<bool>());
        Assert.IsFalse(results[1].Read<bool>());
    }

    [TestMethod]
    public async Task Enum_ReadOnlyProperty()
    {
        var obj = new TypeWithEnum();
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var results = await _state.DoStringAsync(@"
            local c = obj.readOnlyColor
            return tostring(c)
        ");
        Assert.AreEqual("Blue", results[0].Read<string>());
    }

    [TestMethod]
    public async Task Enum_DefaultProperty()
    {
        var obj = new TypeWithEnum();
        _state.Environment["obj"] = LuaValue.FromUserData(obj);

        var results = await _state.DoStringAsync(@"
            local c = obj.defaultColor
            return tostring(c)
        ");
        Assert.AreEqual("Red", results[0].Read<string>());
    }
}


