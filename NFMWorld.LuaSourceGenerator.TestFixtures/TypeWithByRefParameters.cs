using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// Test type with byref parameters - these methods should be excluded from generation
/// </summary>
[LuaVisible]
public class TypeWithByRefParameters
{
    public int Value { get; set; } = 42;

    // Normal method - should be included
    public int GetValue()
    {
        return Value;
    }

    // Method with ref parameter - should be excluded
    public void MethodWithRefParam(ref int value)
    {
        value = Value;
    }

    // Method with out parameter - should be excluded
    public bool TryGetValue(out int value)
    {
        value = Value;
        return true;
    }

    // Method with in parameter - should be excluded
    public void MethodWithInParam(in int value)
    {
        Value = value;
    }

    // Static method with ref parameter - should be excluded
    public static void StaticRefMethod(ref string text)
    {
        text = "modified";
    }

    // Constructor with ref parameter - should be excluded
    public TypeWithByRefParameters(ref int initialValue)
    {
        Value = initialValue;
    }

    // Normal constructor - should be included
    public TypeWithByRefParameters()
    {
    }

    public TypeWithByRefParameters(int value)
    {
        Value = value;
    }
}
