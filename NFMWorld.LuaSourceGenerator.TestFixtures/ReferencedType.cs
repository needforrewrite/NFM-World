namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// A type that is NOT marked with [LuaVisible] but is referenced by other visible types.
/// Should be automatically discovered and have bindings generated.
/// </summary>
public class ReferencedType
{
    public int Value { get; set; }
    public string Name { get; set; } = "";

    public ReferencedType()
    {
    }

    public ReferencedType(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public string GetDescription()
    {
        return $"ReferencedType: {Name} = {Value}";
    }

    public override string ToString()
    {
        return GetDescription();
    }
}
