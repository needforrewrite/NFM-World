using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// A sample class to test Lua bindings for classes.
/// This class has various members to test different binding scenarios.
/// </summary>
[LuaVisible]
public partial class SampleClass
{
    // Static property
    [LuaName] public static int StaticCounter { get; set; } = 0;

    // Static readonly property
    [LuaName] public static string StaticName => "SampleClass";

    // Instance properties
    [LuaName] public int Id { get; set; }
    [LuaName] public string Name { get; set; } = "";
    [LuaName] public bool IsActive { get; set; }
    [LuaName] public float Value { get; set; }
    [LuaName] public double PreciseValue { get; set; }

    // Nullable properties
    [LuaName] public int? NullableInt { get; set; }
    [LuaName] public float? NullableFloat { get; set; }
    [LuaName] public bool? NullableBool { get; set; }

    // Static nullable property
    [LuaName] public static double? StaticNullableDouble { get; set; }

    // Public fields
    [LuaName] public int PublicField;
    [LuaName] public string PublicStringField = "";

    // Nullable field
    [LuaName] public long? NullableLongField;

    // Private field (should not be exposed)
    private int _privateField;

    // Default constructor
    [LuaName] public SampleClass()
    {
    }

    // Parameterized constructor
    [LuaName] public SampleClass(int id, string name)
    {
        Id = id;
        Name = name;
    }

    // Full constructor
    [LuaName] public SampleClass(int id, string name, bool isActive, float value)
    {
        Id = id;
        Name = name;
        IsActive = isActive;
        Value = value;
    }

    // Constructor with nullable parameters
    [LuaName] public SampleClass(int? nullableId, string? nullableName)
    {
        Id = nullableId ?? 0;
        Name = nullableName ?? "";
    }

    // Static method
    [LuaName] public static int Add(int a, int b)
    {
        return a + b;
    }

    // Static method with different types
    [LuaName] public static string Concat(string a, string b)
    {
        return a + b;
    }

    // Static method with side effect
    [LuaName] public static void IncrementCounter()
    {
        StaticCounter++;
    }

    // Static method with nullable parameter
    [LuaName] public static int AddNullable(int? a, int? b)
    {
        return (a ?? 0) + (b ?? 0);
    }

    // Static method returning nullable
    [LuaName] public static int? GetNullableValue(bool hasValue, int value)
    {
        return hasValue ? value : null;
    }

    // Instance method
    [LuaName] public int GetDoubleId()
    {
        return Id * 2;
    }

    // Instance method with parameters
    [LuaName] public string GetGreeting(string prefix)
    {
        return $"{prefix} {Name}!";
    }

    // Instance method that modifies state
    [LuaName] public void SetValue(float newValue)
    {
        Value = newValue;
    }

    // Method with multiple parameters
    [LuaName] public float Calculate(float a, float b, bool multiply)
    {
        return multiply ? a * b : a + b;
    }

    // Method returning another object
    [LuaName] public SampleClass Clone()
    {
        return new SampleClass(Id, Name, IsActive, Value);
    }

    // Instance method with nullable parameter
    [LuaName] public void SetNullableValue(float? newValue)
    {
        Value = newValue ?? 0;
    }

    // Instance method with nullable parameter returning nullable
    [LuaName] public int? MultiplyByNullable(int? multiplier)
    {
        if (!multiplier.HasValue)
            return null;
        return Id * multiplier.Value;
    }

    // Instance method with multiple nullable parameters
    [LuaName] public string FormatWithOptional(string? prefix, string? suffix)
    {
        var p = prefix ?? "";
        var s = suffix ?? "";
        return $"{p}{Name}{s}";
    }

    // Hidden method (should not be exposed)
    [LuaHidden]
    public void HiddenMethod()
    {
        _privateField = 42;
    }

    // Method with custom Lua name
    [LuaName("customName")]
    public string MethodWithCustomName()
    {
        return "custom";
    }

    public override string ToString()
    {
        return $"SampleClass(Id={Id}, Name={Name}, IsActive={IsActive}, Value={Value})";
    }
}
