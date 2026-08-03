using nfm_world_library.Lua;
using System.Collections.Generic;

namespace NFMWorld.LuaSourceGenerator.Test;

[LuaVisible(Name = nameof(TypeWithStringIndexer))]
public class TypeWithStringIndexer
{
    private readonly Dictionary<string, int> _data = new();

    // Single-parameter indexer with string (read-only from Lua side for simplicity)
    public int this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : 0;
        set => _data[key] = value;
    }

    // Constructor
    public TypeWithStringIndexer()
    {
    }

    public void SetValue(string key, int value)
    {
        _data[key] = value;
    }

    public int GetValue(string key)
    {
        return _data.TryGetValue(key, out var value) ? value : 0;
    }

    public int GetDataCount()
    {
        return _data.Count;
    }

    // Static factory method
    public static TypeWithStringIndexer Create()
    {
        return new TypeWithStringIndexer();
    }
}
