using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that record structs get the correct 'record struct' keyword in generated code.
/// </summary>
[LuaVisible]
public partial record struct RecordStructType
{
    [LuaName] public int X { get; set; }
    [LuaName] public int Y { get; set; }

    [LuaName] public RecordStructType()
    {
        X = 0;
        Y = 0;
    }

    [LuaName] public RecordStructType(int x, int y)
    {
        X = x;
        Y = y;
    }

    [LuaName] public int Sum() => X + Y;
}
