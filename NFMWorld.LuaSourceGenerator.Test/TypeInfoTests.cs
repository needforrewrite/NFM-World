using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Tests for the TypeInfo record and attribute handling.
/// </summary>
[TestClass]
public class TypeInfoTests
{
    [TestMethod]
    public void TypeInfo_UsesCustomNameWhenProvided()
    {
        // Arrange
        var type = typeof(SampleStruct);
        // Act
        var typeInfo = new LuaVisibleType(type.Assembly, type);

        // Assert
        Assert.AreEqual("Vec2", typeInfo.LuaName);
    }

    [TestMethod]
    public void TypeInfo_UsesTypeNameWhenNoCustomName()
    {
        // Arrange
        var type = typeof(SampleClass);
        // Act
        var typeInfo = new LuaVisibleType(type.Assembly, type);

        // Assert
        Assert.AreEqual("SampleClass", typeInfo.LuaName);
    }

    [TestMethod]
    public void TypeInfo_StoresTypeReference()
    {
        // Arrange
        var type = typeof(SampleClass);
        // Act
        var typeInfo = new LuaVisibleType(type.Assembly, type);

        // Assert
        Assert.AreEqual(type, typeInfo.Type);
    }
}
