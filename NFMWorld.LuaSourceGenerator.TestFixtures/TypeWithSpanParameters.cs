using System;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that methods with Span/ReadOnlySpan parameters are safely skipped
/// (ref structs can't be generic type arguments, so GetArgument<ReadOnlySpan<T>> would fail).
/// </summary>
[LuaVisible]
public partial class TypeWithSpanParameters
{
    [LuaName] public TypeWithSpanParameters() { }

    [LuaName] public string Name { get; set; } = "";

    // Method with ReadOnlySpan param — should be SKIPPED by the generator
    public int Sum(ReadOnlySpan<int> values)
    {
        int total = 0;
        foreach (var v in values) total += v;
        return total;
    }

    // Method with Span param — should be SKIPPED
    public void Fill(Span<int> values, int fillValue)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] = fillValue;
    }

    // Method returning a ref struct — should be SKIPPED
    public ReadOnlySpan<char> GetChars()
    {
        return Name.AsSpan();
    }

    // Normal method (no ref struct params) — should be INCLUDED
    [LuaName] public string GetName() => Name;

    // Method with mix of normal and ref struct params — should be SKIPPED
    public int CountMatching(ReadOnlySpan<int> values, int target)
    {
        int count = 0;
        foreach (var v in values)
            if (v == target) count++;
        return count;
    }
}
