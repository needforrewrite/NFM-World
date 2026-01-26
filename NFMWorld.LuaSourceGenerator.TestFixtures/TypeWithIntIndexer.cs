using nfm_world_library.Lua;
using System.Collections.Generic;

namespace NFMWorld.LuaSourceGenerator.Test;

[LuaVisible(Name = nameof(TypeWithIntIndexer))]
public class TypeWithIntIndexer
{
    private readonly int[] _numbers = new int[10];

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

    // Constructor
    public TypeWithIntIndexer()
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

    public int GetNumbersLength()
    {
        return _numbers.Length;
    }

    // Static factory method
    public static TypeWithIntIndexer Create()
    {
        return new TypeWithIntIndexer();
    }
}
