using System.Reflection;
using NFMWorld.LuaSourceGenerator.Test.SampleTypes;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Tests for the LuaBindingGenerator code generation.
/// These tests verify that the generator produces correct binding code.
/// </summary>
[TestFixture]
public class LuaBindingGeneratorTests
{
    private LuaBindingGenerator _generator = null!;

    [SetUp]
    public void Setup()
    {
        _generator = new LuaBindingGenerator();
    }

    [Test]
    public void Generate_FindsLuaVisibleTypes()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("SampleClass"));
        Assert.That(code, Does.Contain("Vec2")); // SampleStruct with custom name
        Assert.That(code, Does.Contain("Vec3")); // Vector3Struct with custom name
    }

    [Test]
    public void Generate_CreatesInitializeMethod()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("public static void Initialize(lua_State L)"));
    }

    [Test]
    public void Generate_CreatesMetatableForType()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("luaL_newmetatable"));
        Assert.That(code, Does.Contain("__gc"));
        Assert.That(code, Does.Contain("__index"));
        Assert.That(code, Does.Contain("__newindex"));
        Assert.That(code, Does.Contain("__tostring"));
    }

    [Test]
    public void Generate_CreatesConstructorBinding()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("_new(lua_State L)"));
        Assert.That(code, Does.Contain("\"new\""));
    }

    [Test]
    public void Generate_CreatesStaticMethodBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("_static_add")); // SampleClass.Add
        Assert.That(code, Does.Contain("_static_concat")); // SampleClass.Concat
    }

    [Test]
    public void Generate_CreatesInstanceMethodBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("_method_getDoubleId"));
        Assert.That(code, Does.Contain("_method_getGreeting"));
    }

    [Test]
    public void Generate_CreatesPropertyBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("case \"id\":")); // Id property
        Assert.That(code, Does.Contain("case \"name\":")); // Name property
        Assert.That(code, Does.Contain("case \"isActive\":")); // IsActive property
    }

    [Test]
    public void Generate_CreatesFieldBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("case \"publicField\":")); // PublicField
    }

    [Test]
    public void Generate_RespectsLuaHiddenAttribute()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Not.Contain("hiddenMethod"));
    }

    [Test]
    public void Generate_RespectsLuaNameAttribute()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("case \"customName\":"));
    }

    [Test]
    public void Generate_CreatesOperatorBindingsForStruct()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleStruct).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("__add"));
        Assert.That(code, Does.Contain("__sub"));
        Assert.That(code, Does.Contain("__mul"));
        Assert.That(code, Does.Contain("__div"));
        Assert.That(code, Does.Contain("__unm")); // Unary negation
        Assert.That(code, Does.Contain("__eq")); // Equality
    }

    [Test]
    public void Generate_CreatesStaticPropertyBindings()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("_type__index"));
        Assert.That(code, Does.Contain("_type__newindex"));
        Assert.That(code, Does.Contain("case \"staticCounter\":"));
    }

    [Test]
    public void Generate_HelperMethodsAreInBaseFile()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - Helper methods are now in LuaBindingsBase.cs, so generated code should NOT contain them
        // Instead, generated code should reference them
        Assert.That(code, Does.Not.Contain("private static int StoreObject(object obj)"));
        Assert.That(code, Does.Not.Contain("private static object? GetObject(int id)"));
        Assert.That(code, Does.Not.Contain("private static void RemoveObject(int id)"));

        // But generated code should USE these methods
        Assert.That(code, Does.Contain("PushObject(L, obj"));
        Assert.That(code, Does.Contain("GetObjectFromStack<"));
        Assert.That(code, Does.Contain("GetStructFromStack<"));
        Assert.That(code, Does.Contain("PushValue(L,"));
        Assert.That(code, Does.Contain("ToObject(L,"));
    }

    [Test]
    public void Generate_StructUsesCopySemantics()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleStruct).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        // Structs should use GetStructFromStack and UpdateStruct
        Assert.That(code, Does.Contain("GetStructFromStack<NFMWorld.LuaSourceGenerator.Test.SampleTypes.SampleStruct>"));
        Assert.That(code, Does.Contain("UpdateStruct(L, 1, obj)"));
    }

    [Test]
    public void Generate_UsesCorrectNamespaces()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert
        Assert.That(code, Does.Contain("using LuaNET.LuaJIT;"));
        Assert.That(code, Does.Contain("using static LuaNET.LuaJIT.Lua;"));
        Assert.That(code, Does.Contain("namespace nfm_world.Lua;"));
    }

    [Test]
    public void Generate_OutputIsValidCSharpStructure()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - basic structure validation
        Assert.That(code, Does.Contain("public partial class LuaBindings"));
        Assert.That(code, Does.Contain("{"));
        Assert.That(code.Count(c => c == '{'), Is.EqualTo(code.Count(c => c == '}')),
            "Braces should be balanced");
    }

    [Test]
    public void Generate_HandlesMultipleConstructorOverloads()
    {
        // Arrange
        var assemblies = new[] { typeof(SampleClass).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - SampleClass has 3 constructors
        Assert.That(code, Does.Contain("if (argCount == 0)")); // Default
        Assert.That(code, Does.Contain("if (argCount == 2)")); // (int, string)
        Assert.That(code, Does.Contain("if (argCount == 4)")); // (int, string, bool, float)
    }

    [Test]
    public void Generate_HandlesMethodOverloads()
    {
        // The generator groups methods by name, so overloads should be handled in one function
        var assemblies = new[] { typeof(SampleClass).Assembly };

        var code = _generator.Generate(assemblies);

        // Should have overload handling with argCount checks
        Assert.That(code, Does.Contain("var argCount = lua_gettop(L)"));
    }

    [Test]
    public void Generate_CrossTypeMethodReturnsCorrectType()
    {
        // Arrange
        var assemblies = new[] { typeof(Vector3Struct).Assembly };

        // Act
        var code = _generator.Generate(assemblies);

        // Assert - ToVec2 should push a SampleStruct
        Assert.That(code, Does.Contain("_method_toVec2"));
        Assert.That(code, Does.Contain("PushValue(L, result)"));
    }
}
