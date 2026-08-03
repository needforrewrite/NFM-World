using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// A sample class to test Lua bindings for classes.
/// This class has various members to test different binding scenarios.
/// </summary>
[LuaVisible]
public class SampleClass
{
    // Static property
    public static int StaticCounter { get; set; } = 0;

    // Static readonly property
    public static string StaticName => "SampleClass";

    // Instance properties
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public float Value { get; set; }
    public double PreciseValue { get; set; }

    // Nullable properties
    public int? NullableInt { get; set; }
    public float? NullableFloat { get; set; }
    public bool? NullableBool { get; set; }

    // Static nullable property
    public static double? StaticNullableDouble { get; set; }

    // Public fields
    public int PublicField;
    public string PublicStringField = "";

    // Nullable field
    public long? NullableLongField;

    // Private field (should not be exposed)
    private int _privateField;

    // Default constructor
    public SampleClass()
    {
    }

    // Parameterized constructor
    public SampleClass(int id, string name)
    {
        Id = id;
        Name = name;
    }

    // Full constructor
    public SampleClass(int id, string name, bool isActive, float value)
    {
        Id = id;
        Name = name;
        IsActive = isActive;
        Value = value;
    }

    // Constructor with nullable parameters
    public SampleClass(int? nullableId, string? nullableName)
    {
        Id = nullableId ?? 0;
        Name = nullableName ?? "";
    }

    // Static method
    public static int Add(int a, int b)
    {
        return a + b;
    }

    // Static method with different types
    public static string Concat(string a, string b)
    {
        return a + b;
    }

    // Static method with side effect
    public static void IncrementCounter()
    {
        StaticCounter++;
    }

    // Static method with nullable parameter
    public static int AddNullable(int? a, int? b)
    {
        return (a ?? 0) + (b ?? 0);
    }

    // Static method returning nullable
    public static int? GetNullableValue(bool hasValue, int value)
    {
        return hasValue ? value : null;
    }

    // Instance method
    public int GetDoubleId()
    {
        return Id * 2;
    }

    // Instance method with parameters
    public string GetGreeting(string prefix)
    {
        return $"{prefix} {Name}!";
    }

    // Instance method that modifies state
    public void SetValue(float newValue)
    {
        Value = newValue;
    }

    // Method with multiple parameters
    public float Calculate(float a, float b, bool multiply)
    {
        return multiply ? a * b : a + b;
    }

    // Method returning another object
    public SampleClass Clone()
    {
        return new SampleClass(Id, Name, IsActive, Value);
    }

    // Instance method with nullable parameter
    public void SetNullableValue(float? newValue)
    {
        Value = newValue ?? 0;
    }

    // Instance method with nullable parameter returning nullable
    public int? MultiplyByNullable(int? multiplier)
    {
        if (!multiplier.HasValue)
            return null;
        return Id * multiplier.Value;
    }

    // Instance method with multiple nullable parameters
    public string FormatWithOptional(string? prefix, string? suffix)
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
