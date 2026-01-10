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
        var attr = type.GetCustomAttributes(typeof(LuaVisibleAttribute), false)
            .Cast<LuaVisibleAttribute>()
            .First();

        // Act
        var typeInfo = new TypeInfo(type, attr);

        // Assert
        Assert.AreEqual("Vec2", typeInfo.LuaName);
    }

    [TestMethod]
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
        Assert.AreEqual("SampleClass", typeInfo.LuaName);
    }

    [TestMethod]
    public void TypeInfo_StoresTypeReference()
    {
        // Arrange
        var type = typeof(SampleClass);
        var attr = new LuaVisibleAttribute();

        // Act
        var typeInfo = new TypeInfo(type, attr);

        // Assert
        Assert.AreEqual(type, typeInfo.Type);
    }
}
