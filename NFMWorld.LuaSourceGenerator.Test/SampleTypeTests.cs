using NFMWorld.LuaSourceGenerator.Test.SampleTypes;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Tests for sample types to ensure they behave correctly before Lua integration.
/// </summary>
[TestFixture]
public class SampleTypeTests
{
    // ========== SampleClass Tests ==========

    [Test]
    public void SampleClass_DefaultConstructor_CreatesInstance()
    {
        var obj = new SampleClass();

        Assert.That(obj.Id, Is.EqualTo(0));
        Assert.That(obj.Name, Is.EqualTo(""));
    }

    [Test]
    public void SampleClass_ParameterizedConstructor_SetsValues()
    {
        var obj = new SampleClass(42, "Test");

        Assert.That(obj.Id, Is.EqualTo(42));
        Assert.That(obj.Name, Is.EqualTo("Test"));
    }

    [Test]
    public void SampleClass_StaticAdd_ReturnsSum()
    {
        var result = SampleClass.Add(3, 5);

        Assert.That(result, Is.EqualTo(8));
    }

    [Test]
    public void SampleClass_StaticConcat_ConcatenatesStrings()
    {
        var result = SampleClass.Concat("Hello", "World");

        Assert.That(result, Is.EqualTo("HelloWorld"));
    }

    [Test]
    public void SampleClass_StaticCounter_IncrementsProperly()
    {
        SampleClass.StaticCounter = 0;
        SampleClass.IncrementCounter();
        SampleClass.IncrementCounter();

        Assert.That(SampleClass.StaticCounter, Is.EqualTo(2));
    }

    [Test]
    public void SampleClass_GetDoubleId_ReturnsDoubledId()
    {
        var obj = new SampleClass { Id = 21 };

        Assert.That(obj.GetDoubleId(), Is.EqualTo(42));
    }

    [Test]
    public void SampleClass_GetGreeting_FormatsCorrectly()
    {
        var obj = new SampleClass { Name = "World" };

        Assert.That(obj.GetGreeting("Hello"), Is.EqualTo("Hello World!"));
    }

    [Test]
    public void SampleClass_Clone_CreatesIndependentCopy()
    {
        var original = new SampleClass(1, "Original", true, 3.14f);
        var clone = original.Clone();

        clone.Id = 2;
        clone.Name = "Clone";

        Assert.That(original.Id, Is.EqualTo(1));
        Assert.That(original.Name, Is.EqualTo("Original"));
        Assert.That(clone.Id, Is.EqualTo(2));
        Assert.That(clone.Name, Is.EqualTo("Clone"));
    }

    // ========== SampleStruct Tests ==========

    [Test]
    public void SampleStruct_DefaultConstructor_CreatesZero()
    {
        var v = new SampleStruct();

        Assert.That(v.X, Is.EqualTo(0));
        Assert.That(v.Y, Is.EqualTo(0));
    }

    [Test]
    public void SampleStruct_ParameterizedConstructor_SetsValues()
    {
        var v = new SampleStruct(3.0f, 4.0f);

        Assert.That(v.X, Is.EqualTo(3.0f));
        Assert.That(v.Y, Is.EqualTo(4.0f));
    }

    [Test]
    public void SampleStruct_Length_CalculatesCorrectly()
    {
        var v = new SampleStruct(3.0f, 4.0f);

        Assert.That(v.Length, Is.EqualTo(5.0f));
    }

    [Test]
    public void SampleStruct_Distance_CalculatesCorrectly()
    {
        var a = new SampleStruct(0, 0);
        var b = new SampleStruct(3, 4);

        Assert.That(SampleStruct.Distance(a, b), Is.EqualTo(5.0f));
    }

    [Test]
    public void SampleStruct_Dot_CalculatesCorrectly()
    {
        var a = new SampleStruct(1, 2);
        var b = new SampleStruct(3, 4);

        Assert.That(SampleStruct.Dot(a, b), Is.EqualTo(11.0f)); // 1*3 + 2*4
    }

    [Test]
    public void SampleStruct_Normalized_ReturnsUnitVector()
    {
        var v = new SampleStruct(3.0f, 4.0f);
        var n = v.Normalized();

        Assert.That(n.Length, Is.EqualTo(1.0f).Within(0.0001f));
        Assert.That(n.X, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(n.Y, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void SampleStruct_Addition_Works()
    {
        var a = new SampleStruct(1, 2);
        var b = new SampleStruct(3, 4);
        var result = a + b;

        Assert.That(result.X, Is.EqualTo(4));
        Assert.That(result.Y, Is.EqualTo(6));
    }

    [Test]
    public void SampleStruct_Subtraction_Works()
    {
        var a = new SampleStruct(5, 7);
        var b = new SampleStruct(2, 3);
        var result = a - b;

        Assert.That(result.X, Is.EqualTo(3));
        Assert.That(result.Y, Is.EqualTo(4));
    }

    [Test]
    public void SampleStruct_Multiplication_Works()
    {
        var v = new SampleStruct(2, 3);
        var result = v * 4;

        Assert.That(result.X, Is.EqualTo(8));
        Assert.That(result.Y, Is.EqualTo(12));
    }

    [Test]
    public void SampleStruct_Division_Works()
    {
        var v = new SampleStruct(8, 12);
        var result = v / 4;

        Assert.That(result.X, Is.EqualTo(2));
        Assert.That(result.Y, Is.EqualTo(3));
    }

    [Test]
    public void SampleStruct_Negation_Works()
    {
        var v = new SampleStruct(3, -4);
        var result = -v;

        Assert.That(result.X, Is.EqualTo(-3));
        Assert.That(result.Y, Is.EqualTo(4));
    }

    [Test]
    public void SampleStruct_Equality_Works()
    {
        var a = new SampleStruct(1, 2);
        var b = new SampleStruct(1, 2);
        var c = new SampleStruct(3, 4);

        Assert.That(a == b, Is.True);
        Assert.That(a == c, Is.False);
        Assert.That(a != c, Is.True);
    }

    [Test]
    public void SampleStruct_StaticProperties_ReturnCorrectValues()
    {
        Assert.That(SampleStruct.Zero.X, Is.EqualTo(0));
        Assert.That(SampleStruct.Zero.Y, Is.EqualTo(0));
        Assert.That(SampleStruct.One.X, Is.EqualTo(1));
        Assert.That(SampleStruct.One.Y, Is.EqualTo(1));
        Assert.That(SampleStruct.UnitX.X, Is.EqualTo(1));
        Assert.That(SampleStruct.UnitX.Y, Is.EqualTo(0));
    }

    [Test]
    public void SampleStruct_Set_ModifiesInPlace()
    {
        var v = new SampleStruct(1, 2);
        v.Set(5, 6);

        Assert.That(v.X, Is.EqualTo(5));
        Assert.That(v.Y, Is.EqualTo(6));
    }

    [Test]
    public void SampleStruct_ToString_FormatsCorrectly()
    {
        var v = new SampleStruct(1.5f, 2.5f);

        Assert.That(v.ToString(), Does.Contain("1.5"));
        Assert.That(v.ToString(), Does.Contain("2.5"));
    }

    // ========== Vector3Struct Tests ==========

    [Test]
    public void Vector3Struct_Cross_CalculatesCorrectly()
    {
        var a = new Vector3Struct(1, 0, 0);
        var b = new Vector3Struct(0, 1, 0);
        var result = Vector3Struct.Cross(a, b);

        Assert.That(result.X, Is.EqualTo(0).Within(0.0001f));
        Assert.That(result.Y, Is.EqualTo(0).Within(0.0001f));
        Assert.That(result.Z, Is.EqualTo(1).Within(0.0001f));
    }

    [Test]
    public void Vector3Struct_ToVec2_ExtractsXY()
    {
        var v3 = new Vector3Struct(1, 2, 3);
        var v2 = v3.ToVec2();

        Assert.That(v2.X, Is.EqualTo(1));
        Assert.That(v2.Y, Is.EqualTo(2));
    }

    [Test]
    public void Vector3Struct_FromVec2_CreatesVec3()
    {
        var v2 = new SampleStruct(1, 2);
        var v3 = Vector3Struct.FromVec2(v2, 3);

        Assert.That(v3.X, Is.EqualTo(1));
        Assert.That(v3.Y, Is.EqualTo(2));
        Assert.That(v3.Z, Is.EqualTo(3));
    }
}
