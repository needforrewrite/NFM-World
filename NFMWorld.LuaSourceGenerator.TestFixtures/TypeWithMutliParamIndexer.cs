using nfm_world_library.Lua;
using System.Collections.Generic;

namespace NFMWorld.LuaSourceGenerator.Test;

[LuaVisible(Name = nameof(TypeWithMultiParamIndexer))]
public class TypeWithMultiParamIndexer
{
    private readonly Dictionary<(int, int), string> _grid = new();

    // Multi-parameter indexer (2D grid)
    public string this[int row, int col]
    {
        get => _grid.TryGetValue((row, col), out var value) ? value : "";
        set => _grid[(row, col)] = value;
    }

    // Constructor
    public TypeWithMultiParamIndexer()
    {
    }

    public void SetGridValue(int row, int col, string value)
    {
        _grid[(row, col)] = value;
    }

    public string GetGridValue(int row, int col)
    {
        return _grid.TryGetValue((row, col), out var value) ? value : "";
    }

    public int GetGridCount()
    {
        return _grid.Count;
    }

    // Static factory method
    public static TypeWithMultiParamIndexer Create()
    {
        return new TypeWithMultiParamIndexer();
    }
}
