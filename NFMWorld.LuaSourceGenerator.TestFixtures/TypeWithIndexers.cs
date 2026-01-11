using nfm_world_library.Lua;
using System.Collections.Generic;

namespace NFMWorld.LuaSourceGenerator.Test;

[LuaVisible(Name = "TypeWithIndexers")]
public class TypeWithIndexers
{
    private readonly Dictionary<string, int> _data = new();
    private readonly int[] _numbers = new int[10];
    private readonly Dictionary<(int, int), string> _grid = new();

    // Single-parameter indexer with int
    public int this[int index]
    {
        get => index >= 0 && index < _numbers.Length ? _numbers[index] : -1;
        set
        {
            if (index >= 0 && index < _numbers.Length)
                _numbers[index] = value;
        }
    }

    // Single-parameter indexer with string (read-only from Lua side for simplicity)
    public int this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : 0;
        set => _data[key] = value;
    }

    // Multi-parameter indexer (2D grid)
    public string this[int row, int col]
    {
        get => _grid.TryGetValue((row, col), out var value) ? value : "";
        set => _grid[(row, col)] = value;
    }

    // Constructor
    public TypeWithIndexers()
    {
    }

    // Methods for testing
    public void SetNumberAt(int index, int value)
    {
        if (index >= 0 && index < _numbers.Length)
            _numbers[index] = value;
    }

    public int GetNumberAt(int index)
    {
        return index >= 0 && index < _numbers.Length ? _numbers[index] : -1;
    }

    public void SetValue(string key, int value)
    {
        _data[key] = value;
    }

    public int GetValue(string key)
    {
        return _data.TryGetValue(key, out var value) ? value : 0;
    }

    public void SetGridValue(int row, int col, string value)
    {
        _grid[(row, col)] = value;
    }

    public string GetGridValue(int row, int col)
    {
        return _grid.TryGetValue((row, col), out var value) ? value : "";
    }

    public int GetNumbersLength()
    {
        return _numbers.Length;
    }

    public int GetDataCount()
    {
        return _data.Count;
    }

    public int GetGridCount()
    {
        return _grid.Count;
    }

    // Static factory method
    public static TypeWithIndexers Create()
    {
        return new TypeWithIndexers();
    }
}
