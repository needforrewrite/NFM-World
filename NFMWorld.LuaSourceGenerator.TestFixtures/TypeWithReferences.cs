using System.Collections.Generic;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// A type that references other types (both non-generic and generic).
/// Tests automatic type discovery.
/// </summary>
[LuaVisible]
public class TypeWithReferences
{
    // Reference to a non-generic type (should be auto-discovered)
    public ReferencedType? Referenced { get; set; }

    // Generic property (should auto-discover List<int>)
    public List<int>? Numbers { get; set; }

    // Generic property with referenced type (should auto-discover List<ReferencedType>)
    public List<ReferencedType>? Items { get; set; }

    public TypeWithReferences()
    {
    }

    // Constructor with referenced type parameter
    public TypeWithReferences(ReferencedType referenced)
    {
        Referenced = referenced;
    }

    // Method returning referenced type
    public ReferencedType CreateReferenced(int value, string name)
    {
        return new ReferencedType(value, name);
    }

    // Method with generic parameter
    public List<int> CreateNumberList(int count)
    {
        var list = new List<int>();
        for (int i = 0; i < count; i++)
        {
            list.Add(i);
        }
        return list;
    }

    // Method with generic referenced type
    public List<ReferencedType> GetItems()
    {
        return Items ?? new List<ReferencedType>();
    }

    // Method accepting generic parameter
    public int SumNumbers(List<int> numbers)
    {
        var sum = 0;
        foreach (var n in numbers)
        {
            sum += n;
        }
        return sum;
    }

    public override string ToString()
    {
        return $"TypeWithReferences(Numbers: {Numbers?.Count ?? 0}, Items: {Items?.Count ?? 0})";
    }
}
