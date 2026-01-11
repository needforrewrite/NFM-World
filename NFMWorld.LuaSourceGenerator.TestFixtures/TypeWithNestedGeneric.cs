using nfm_world_library.Lua;
using System.Collections.Generic;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// Test type that uses nested generic types like List<int>.Enumerator
/// </summary>
[LuaVisible]
public class TypeWithNestedGeneric
{
    private List<int> _numbers = new List<int> { 1, 2, 3, 4, 5 };

    public List<int> Numbers => _numbers;

    // This method returns List<int>.Enumerator, which should be discovered and generated
    public List<int>.Enumerator GetEnumerator()
    {
        return _numbers.GetEnumerator();
    }

    // Test with string list too
    public List<string> GetStringList()
    {
        return new List<string> { "a", "b", "c" };
    }

    // Returns the enumerator of a string list
    public List<string>.Enumerator GetStringEnumerator()
    {
        var list = new List<string> { "x", "y", "z" };
        return list.GetEnumerator();
    }
}
