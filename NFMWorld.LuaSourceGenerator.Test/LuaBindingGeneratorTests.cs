using System.Reflection;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Tests for the LuaBindingGenerator code generation.
/// These tests verify that the generator produces correct binding code.
/// </summary>
[TestClass]
public class LuaBindingGeneratorTests
{
    private LuaBindingGenerator _generator = null!;

    [TestInitialize]
    public void Setup()
    {
        _generator = new LuaBindingGenerator();
    }

    [TestMethod]
    public void Generate_FindsLuaVisibleTypes()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("SampleClass"));
        Assert.IsTrue(code.Contains("Vec2")); // SampleStruct with custom name
        Assert.IsTrue(code.Contains("Vec3")); // Vector3Struct with custom name
    }

    [TestMethod]
    public void Generate_CreatesInitializeMethod()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("public static void Initialize(lua_State L)"));
    }

    [TestMethod]
    public void Generate_CreatesMetatableForType()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("luaL_newmetatable"));
        Assert.IsTrue(code.Contains("__gc"));
        Assert.IsTrue(code.Contains("__index"));
        Assert.IsTrue(code.Contains("__newindex"));
        Assert.IsTrue(code.Contains("__tostring"));
    }

    [TestMethod]
    public void Generate_CreatesConstructorBinding()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("_new(lua_State L)"));
        Assert.IsTrue(code.Contains("\"new\""));
    }

    [TestMethod]
    public void Generate_CreatesStaticMethodBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("_static_add")); // SampleClass.Add
        Assert.IsTrue(code.Contains("_static_concat")); // SampleClass.Concat
    }

    [TestMethod]
    public void Generate_CreatesInstanceMethodBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("_method_getDoubleId"));
        Assert.IsTrue(code.Contains("_method_getGreeting"));
    }

    [TestMethod]
    public void Generate_CreatesPropertyBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("case \"id\":")); // Id property
        Assert.IsTrue(code.Contains("case \"name\":")); // Name property
        Assert.IsTrue(code.Contains("case \"isActive\":")); // IsActive property
    }

    [TestMethod]
    public void Generate_CreatesFieldBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("case \"publicField\":")); // PublicField
    }

    [TestMethod]
    public void Generate_RespectsLuaHiddenAttribute()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsFalse(code.Contains("hiddenMethod"));
    }

    [TestMethod]
    public void Generate_RespectsLuaNameAttribute()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("case \"customName\":"));
    }

    [TestMethod]
    public void Generate_CreatesOperatorBindingsForStruct()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleStruct).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("__add"));
        Assert.IsTrue(code.Contains("__sub"));
        Assert.IsTrue(code.Contains("__mul"));
        Assert.IsTrue(code.Contains("__div"));
        Assert.IsTrue(code.Contains("__unm")); // Unary negation
        Assert.IsTrue(code.Contains("__eq")); // Equality
    }

    [TestMethod]
    public void Generate_CreatesStaticPropertyBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("_type__index"));
        Assert.IsTrue(code.Contains("_type__newindex"));
        Assert.IsTrue(code.Contains("case \"staticCounter\":"));
    }

    [TestMethod]
    public void Generate_HelperMethodsAreInBaseFile()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - Helper methods are now in LuaBindingsBase.cs, so generated code should NOT contain them
        // Instead, generated code should reference them
        Assert.IsFalse(code.Contains("private static int StoreObject(object obj)"));
        Assert.IsFalse(code.Contains("private static object? GetObject(int id)"));
        Assert.IsFalse(code.Contains("private static void RemoveObject(int id)"));

        // But generated code should USE these methods
        Assert.IsTrue(code.Contains("PushObject(L, obj"));
        Assert.IsTrue(code.Contains("GetObjectFromStack<"));
        Assert.IsTrue(code.Contains("GetStructFromStack<"));
        Assert.IsTrue(code.Contains("PushValue(L,"));
        Assert.IsTrue(code.Contains("ToObject(L,"));
    }

    [TestMethod]
    public void Generate_StructUsesCopySemantics()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleStruct).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        // Structs should use GetStructFromStack and UpdateStruct
        Assert.IsTrue(code.Contains("GetStructFromStack<NFMWorld.LuaSourceGenerator.Test.SampleTypes.SampleStruct>"));
        Assert.IsTrue(code.Contains("UpdateStruct(L, 1, obj)"));
    }

    [TestMethod]
    public void Generate_UsesCorrectNamespaces()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.IsTrue(code.Contains("using LuaNET.LuaJIT;"));
        Assert.IsTrue(code.Contains("using static LuaNET.LuaJIT.Lua;"));
        Assert.IsTrue(code.Contains("namespace nfm_world.Lua;"));
    }

    [TestMethod]
    public void Generate_OutputIsValidCSharpStructure()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - basic structure validation
        Assert.IsTrue(code.Contains("public partial class LuaBindings"));
        Assert.IsTrue(code.Contains("{"));
        Assert.AreEqual(code.Count(c => c == '{'), code.Count(c => c == '}'),
            "Braces should be balanced");
    }

    [TestMethod]
    public void Generate_HandlesMultipleConstructorOverloads()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - SampleClass has 3 constructors
        Assert.IsTrue(code.Contains("if (argCount == 0)")); // Default
        Assert.IsTrue(code.Contains("if (argCount == 2)")); // (int, string)
        Assert.IsTrue(code.Contains("if (argCount == 4)")); // (int, string, bool, float)
    }

    [TestMethod]
    public void Generate_HandlesMethodOverloads()
    {
        // The generator groups methods by name, so overloads should be handled in one function
        var assemblies = new[] { typeof(SampleClass).Assembly };

        var code = _generator.Generate(assemblies);

        // Should have overload handling with argCount checks
        Assert.IsTrue(code.Contains("var argCount = lua_gettop(L)"));
    }

    [TestMethod]
    public void Generate_CrossTypeMethodReturnsCorrectType()
    {
        // Arrange
        var assemblies = new[] { typeof(Vector3Struct).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - ToVec2 should push a SampleStruct
        Assert.IsTrue(code.Contains("_method_toVec2"));
        Assert.IsTrue(code.Contains("PushValue(L, result)"));
    }
}
