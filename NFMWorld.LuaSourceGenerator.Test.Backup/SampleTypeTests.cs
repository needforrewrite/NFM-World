using NFMWorld.LuaSourceGenerator.Test.SampleTypes;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Tests for sample types to ensure they behave correctly before Lua integration.
/// </summary>
[TestClass]
public class SampleTypeTests
{
    // ========== SampleClass Tests ==========

    [TestMethod]
    public void SampleClass_DefaultConstructor_CreatesInstance()
    {
        var obj = new SampleClass();

        Assert.AreEqual(0, obj.Id);
        Assert.AreEqual("", obj.Name);
    }

    [TestMethod]
    public void SampleClass_ParameterizedConstructor_SetsValues()
    {
        var obj = new SampleClass(42, "Test");

        Assert.AreEqual(42, obj.Id);
        Assert.AreEqual("Test", obj.Name);
    }

    [TestMethod]
    public void SampleClass_StaticAdd_ReturnsSum()
    {
        var result = SampleClass.Add(3, 5);

        Assert.AreEqual(8, result);
    }

    [TestMethod]
    public void SampleClass_StaticConcat_ConcatenatesStrings()
    {
        var result = SampleClass.Concat("Hello", "World");

        Assert.AreEqual("HelloWorld", result);
    }

    [TestMethod]
    public void SampleClass_StaticCounter_IncrementsProperly()
    {
        SampleClass.StaticCounter = 0;
        SampleClass.IncrementCounter();
        SampleClass.IncrementCounter();

        Assert.AreEqual(2, SampleClass.StaticCounter);
    }

    [TestMethod]
    public void SampleClass_GetDoubleId_ReturnsDoubledId()
    {
        var obj = new SampleClass { Id = 21 };

        Assert.AreEqual(42, obj.GetDoubleId());
    }

    [TestMethod]
    public void SampleClass_GetGreeting_FormatsCorrectly()
    {
        var obj = new SampleClass { Name = "World" };

        Assert.AreEqual("Hello World!", obj.GetGreeting("Hello"));
    }

    [TestMethod]
    public void SampleClass_Clone_CreatesIndependentCopy()
    {
        var original = new SampleClass(1, "Original", true, 3.14f);
        var clone = original.Clone();

        clone.Id = 2;
        clone.Name = "Clone";

        Assert.AreEqual(1, original.Id);
        Assert.AreEqual("Original", original.Name);
        Assert.AreEqual(2, clone.Id);
        Assert.AreEqual("Clone", clone.Name);
    }

    // ========== SampleStruct Tests ==========

    [TestMethod]
    public void SampleStruct_DefaultConstructor_CreatesZero()
    {
        var v = new SampleStruct();

        Assert.AreEqual(0, v.X);
        Assert.AreEqual(0, v.Y);
    }

    [TestMethod]
    public void SampleStruct_ParameterizedConstructor_SetsValues()
    {
        var v = new SampleStruct(3.0f, 4.0f);

        Assert.AreEqual(3.0f, v.X);
        Assert.AreEqual(4.0f, v.Y);
    }

    [TestMethod]
    public void SampleStruct_Length_CalculatesCorrectly()
    {
        var v = new SampleStruct(3.0f, 4.0f);

        Assert.AreEqual(5.0f, v.Length);
    }

    [TestMethod]
    public void SampleStruct_Distance_CalculatesCorrectly()
    {
        var a = new SampleStruct(0, 0);
        var b = new SampleStruct(3, 4);

        Assert.AreEqual(5.0f, SampleStruct.Distance(a, b));
    }

    [TestMethod]
    public void SampleStruct_Dot_CalculatesCorrectly()
    {
        var a = new SampleStruct(1, 2);
        var b = new SampleStruct(3, 4);

        Assert.AreEqual(11.0f, SampleStruct.Dot(a, b)); // 1*3 + 2*4
    }

    [TestMethod]
    public void SampleStruct_Normalized_ReturnsUnitVector()
    {
        var v = new SampleStruct(3.0f, 4.0f);
        var n = v.Normalized();

        Assert.AreEqual(1.0f, n.Length, 0.0001f);
        Assert.AreEqual(0.6f, n.X, 0.0001f);
        Assert.AreEqual(0.8f, n.Y, 0.0001f);
    }

    [TestMethod]
    public void SampleStruct_Addition_Works()
    {
        var a = new SampleStruct(1, 2);
        var b = new SampleStruct(3, 4);
        var result = a + b;

        Assert.AreEqual(4, result.X);
        Assert.AreEqual(6, result.Y);
    }

    [TestMethod]
    public void SampleStruct_Subtraction_Works()
    {
        var a = new SampleStruct(5, 7);
        var b = new SampleStruct(2, 3);
        var result = a - b;

        Assert.AreEqual(3, result.X);
        Assert.AreEqual(4, result.Y);
    }

    [TestMethod]
    public void SampleStruct_Multiplication_Works()
    {
        var v = new SampleStruct(2, 3);
        var result = v * 4;

        Assert.AreEqual(8, result.X);
        Assert.AreEqual(12, result.Y);
    }

    [TestMethod]
    public void SampleStruct_Division_Works()
    {
        var v = new SampleStruct(8, 12);
        var result = v / 4;

        Assert.AreEqual(2, result.X);
        Assert.AreEqual(3, result.Y);
    }

    [TestMethod]
    public void SampleStruct_Negation_Works()
    {
        var v = new SampleStruct(3, -4);
        var result = -v;

        Assert.AreEqual(-3, result.X);
        Assert.AreEqual(4, result.Y);
    }

    [TestMethod]
    public void SampleStruct_Equality_Works()
    {
        var a = new SampleStruct(1, 2);
        var b = new SampleStruct(1, 2);
        var c = new SampleStruct(3, 4);

        Assert.IsTrue(a == b);
        Assert.IsFalse(a == c);
        Assert.IsTrue(a != c);
    }

    [TestMethod]
    public void SampleStruct_StaticProperties_ReturnCorrectValues()
    {
        Assert.AreEqual(0, SampleStruct.Zero.X);
        Assert.AreEqual(0, SampleStruct.Zero.Y);
        Assert.AreEqual(1, SampleStruct.One.X);
        Assert.AreEqual(1, SampleStruct.One.Y);
        Assert.AreEqual(1, SampleStruct.UnitX.X);
        Assert.AreEqual(0, SampleStruct.UnitX.Y);
    }

    [TestMethod]
    public void SampleStruct_Set_ModifiesInPlace()
    {
        var v = new SampleStruct(1, 2);
        v.Set(5, 6);

        Assert.AreEqual(5, v.X);
        Assert.AreEqual(6, v.Y);
    }

    [TestMethod]
    public void SampleStruct_ToString_FormatsCorrectly()
    {
        var v = new SampleStruct(1.5f, 2.5f);

        Assert.IsTrue(v.ToString()!.Contains("1.5"));
        Assert.IsTrue(v.ToString()!.Contains("2.5"));
    }

    // ========== Vector3Struct Tests ==========

    [TestMethod]
    public void Vector3Struct_Cross_CalculatesCorrectly()
    {
        var a = new Vector3Struct(1, 0, 0);
        var b = new Vector3Struct(0, 1, 0);
        var result = Vector3Struct.Cross(a, b);

        Assert.AreEqual(0, result.X, 0.0001f);
        Assert.AreEqual(0, result.Y, 0.0001f);
        Assert.AreEqual(1, result.Z, 0.0001f);
    }

    [TestMethod]
    public void Vector3Struct_ToVec2_ExtractsXY()
    {
        var v3 = new Vector3Struct(1, 2, 3);
        var v2 = v3.ToVec2();

        Assert.AreEqual(1, v2.X);
        Assert.AreEqual(2, v2.Y);
    }

    [TestMethod]
    public void Vector3Struct_FromVec2_CreatesVec3()
    {
        var v2 = new SampleStruct(1, 2);
        var v3 = Vector3Struct.FromVec2(v2, 3);

        Assert.AreEqual(1, v3.X);
        Assert.AreEqual(2, v3.Y);
        Assert.AreEqual(3, v3.Z);
    }

    // ========== Nullable Tests ==========

    [TestMethod]
    public void SampleClass_NullableInt_DefaultIsNull()
    {
        var obj = new SampleClass();

        Assert.IsFalse(obj.NullableInt.HasValue);
        Assert.IsNull(obj.NullableInt);
    }

    [TestMethod]
    public void SampleClass_NullableInt_SetAndGet()
    {
        var obj = new SampleClass { NullableInt = 42 };

        Assert.IsTrue(obj.NullableInt.HasValue);
        Assert.AreEqual(42, obj.NullableInt.Value);
        Assert.AreEqual(42, obj.NullableInt);
    }

    [TestMethod]
    public void SampleClass_NullableFloat_SetToNull()
    {
        var obj = new SampleClass { NullableFloat = 3.14f };
        Assert.IsTrue(obj.NullableFloat.HasValue);

        obj.NullableFloat = null;
        Assert.IsFalse(obj.NullableFloat.HasValue);
    }

    [TestMethod]
    public void SampleClass_NullableBool_TrueFalseNull()
    {
        var obj = new SampleClass();

        obj.NullableBool = true;
        Assert.AreEqual(true, obj.NullableBool);

        obj.NullableBool = false;
        Assert.AreEqual(false, obj.NullableBool);

        obj.NullableBool = null;
        Assert.IsNull(obj.NullableBool);
    }

    [TestMethod]
    public void SampleClass_NullableLongField_DefaultIsNull()
    {
        var obj = new SampleClass();

        Assert.IsFalse(obj.NullableLongField.HasValue);
        Assert.IsNull(obj.NullableLongField);
    }

    [TestMethod]
    public void SampleClass_NullableLongField_SetAndClear()
    {
        var obj = new SampleClass { NullableLongField = 123456789L };

        Assert.IsTrue(obj.NullableLongField.HasValue);
        Assert.AreEqual(123456789L, obj.NullableLongField.Value);

        obj.NullableLongField = null;
        Assert.IsNull(obj.NullableLongField);
    }

    [TestMethod]
    public void SampleClass_StaticNullableDouble_SetAndGet()
    {
        SampleClass.StaticNullableDouble = null;
        Assert.IsNull(SampleClass.StaticNullableDouble);

        SampleClass.StaticNullableDouble = 2.71828;
        Assert.IsTrue(SampleClass.StaticNullableDouble.HasValue);
        Assert.AreEqual(2.71828, SampleClass.StaticNullableDouble.Value, 0.00001);

        SampleClass.StaticNullableDouble = null;
        Assert.IsNull(SampleClass.StaticNullableDouble);
    }

    // ========== Nullable Parameter Tests ==========

    [TestMethod]
    public void SampleClass_ConstructorWithNullableParams_NullValues()
    {
        var obj = new SampleClass(null, null);

        Assert.AreEqual(0, obj.Id);
        Assert.AreEqual("", obj.Name);
    }

    [TestMethod]
    public void SampleClass_ConstructorWithNullableParams_WithValues()
    {
        var obj = new SampleClass(42, "Test");

        Assert.AreEqual(42, obj.Id);
        Assert.AreEqual("Test", obj.Name);
    }

    [TestMethod]
    public void SampleClass_AddNullable_BothNull()
    {
        var result = SampleClass.AddNullable(null, null);
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void SampleClass_AddNullable_OneNull()
    {
        var result = SampleClass.AddNullable(5, null);
        Assert.AreEqual(5, result);

        result = SampleClass.AddNullable(null, 10);
        Assert.AreEqual(10, result);
    }

    [TestMethod]
    public void SampleClass_AddNullable_BothValues()
    {
        var result = SampleClass.AddNullable(5, 10);
        Assert.AreEqual(15, result);
    }

    [TestMethod]
    public void SampleClass_GetNullableValue_ReturnsNull()
    {
        var result = SampleClass.GetNullableValue(false, 42);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SampleClass_GetNullableValue_ReturnsValue()
    {
        var result = SampleClass.GetNullableValue(true, 42);
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void SampleClass_SetNullableValue_WithNull()
    {
        var obj = new SampleClass { Value = 100 };
        obj.SetNullableValue(null);
        Assert.AreEqual(0, obj.Value);
    }

    [TestMethod]
    public void SampleClass_SetNullableValue_WithValue()
    {
        var obj = new SampleClass();
        obj.SetNullableValue(3.14f);
        Assert.AreEqual(3.14f, obj.Value, 0.001f);
    }

    [TestMethod]
    public void SampleClass_MultiplyByNullable_WithNull()
    {
        var obj = new SampleClass { Id = 10 };
        var result = obj.MultiplyByNullable(null);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SampleClass_MultiplyByNullable_WithValue()
    {
        var obj = new SampleClass { Id = 10 };
        var result = obj.MultiplyByNullable(5);
        Assert.AreEqual(50, result);
    }

    [TestMethod]
    public void SampleClass_FormatWithOptional_BothNull()
    {
        var obj = new SampleClass { Name = "Test" };
        var result = obj.FormatWithOptional(null, null);
        Assert.AreEqual("Test", result);
    }

    [TestMethod]
    public void SampleClass_FormatWithOptional_WithValues()
    {
        var obj = new SampleClass { Name = "Test" };
        var result = obj.FormatWithOptional("[", "]");
        Assert.AreEqual("[Test]", result);
    }

    // ========== Referenced Type Tests ==========

    [TestMethod]
    public void ReferencedType_Constructor_SetsValues()
    {
        var obj = new ReferencedType(42, "Test");

        Assert.AreEqual(42, obj.Value);
        Assert.AreEqual("Test", obj.Name);
    }

    [TestMethod]
    public void ReferencedType_GetDescription_ReturnsFormatted()
    {
        var obj = new ReferencedType(10, "Sample");
        var desc = obj.GetDescription();

        Assert.AreEqual("ReferencedType: Sample = 10", desc);
    }

    [TestMethod]
    public void TypeWithReferences_CreateReferenced_ReturnsNewInstance()
    {
        var obj = new TypeWithReferences();
        var referenced = obj.CreateReferenced(99, "Created");

        Assert.AreEqual(99, referenced.Value);
        Assert.AreEqual("Created", referenced.Name);
    }

    [TestMethod]
    public void TypeWithReferences_CreateNumberList_CreatesListWithCount()
    {
        var obj = new TypeWithReferences();
        var numbers = obj.CreateNumberList(5);

        Assert.AreEqual(5, numbers.Count);
        Assert.AreEqual(0, numbers[0]);
        Assert.AreEqual(4, numbers[4]);
    }

    [TestMethod]
    public void TypeWithReferences_SumNumbers_CalculatesSum()
    {
        var obj = new TypeWithReferences();
        var numbers = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 5 };
        var sum = obj.SumNumbers(numbers);

        Assert.AreEqual(15, sum);
    }

    // Tests for TypeWithArrays
    [TestMethod]
    public void TypeWithArrays_Constructor_InitializesEmptyArrays()
    {
        var obj = new TypeWithArrays();

        Assert.IsNull(obj.Numbers);
        Assert.IsNull(obj.Names);
        Assert.IsNull(obj.Values);
    }

    [TestMethod]
    public void TypeWithArrays_ConstructorWithNumbers_SetsNumbers()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var obj = new TypeWithArrays(numbers);

        Assert.AreEqual(5, obj.Numbers!.Length);
        Assert.AreEqual(1, obj.Numbers[0]);
        Assert.AreEqual(5, obj.Numbers[4]);
    }

    [TestMethod]
    public void TypeWithArrays_SumNumbers_CalculatesSum()
    {
        var obj = new TypeWithArrays(new[] { 10, 20, 30 });
        var sum = obj.SumNumbers();

        Assert.AreEqual(60, sum);
    }

    [TestMethod]
    public void TypeWithArrays_ConcatenateNames_JoinsStrings()
    {
        var obj = new TypeWithArrays { Names = new[] { "Alice", "Bob", "Charlie" } };
        var result = obj.ConcatenateNames();

        Assert.AreEqual("Alice, Bob, Charlie", result);
    }

    [TestMethod]
    public void TypeWithArrays_GetAt_ReturnsElement()
    {
        var obj = new TypeWithArrays(new[] { 100, 200, 300 });
        var value = obj.GetAt(1);

        Assert.AreEqual(200, value);
    }

    [TestMethod]
    public void TypeWithArrays_CreateSequence_ReturnsSequentialArray()
    {
        var arr = TypeWithArrays.CreateSequence(5);

        Assert.AreEqual(5, arr.Length);
        Assert.AreEqual(1, arr[0]);
        Assert.AreEqual(5, arr[4]);
    }
}

