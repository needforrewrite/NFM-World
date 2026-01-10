using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Tests for the TypeInfo record and attribute handling.
/// </summary>
[TestFixture]
public class TypeInfoTests
{
    [Test]
    public void TypeInfo_UsesCustomNameWhenProvided()
    {
        // Arrange
        var type = typeof(SampleStruct);
        var attr = type.GetCustomAttributes(typeof(LuaVisibleAttribute), false)
            .Cast<LuaVisibleAttribute>()
            .First();

        // Act
        var typeInfo = new TypeInfo(type, attr);

        // Assert
        Assert.That(typeInfo.LuaName, Is.EqualTo("Vec2"));
    }

    [Test]
    public void TypeInfo_UsesTypeNameWhenNoCustomName()
    {
        // Arrange
        var type = typeof(SampleClass);
        var attr = type.GetCustomAttributes(typeof(LuaVisibleAttribute), false)
            .Cast<LuaVisibleAttribute>()
            .First();

        // Act
        var typeInfo = new TypeInfo(type, attr);

        // Assert
        Assert.That(typeInfo.LuaName, Is.EqualTo("SampleClass"));
    }

    [Test]
    public void TypeInfo_StoresTypeReference()
    {
        // Arrange
        var type = typeof(SampleClass);
        var attr = new LuaVisibleAttribute();

        // Act
        var typeInfo = new TypeInfo(type, attr);

        // Assert
        Assert.That(typeInfo.Type, Is.EqualTo(type));
    }
}
