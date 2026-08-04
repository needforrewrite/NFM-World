using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that record structs get the correct 'record struct' keyword in generated code.
/// </summary>
[LuaVisible]
public partial record struct RecordStructType
{
    public int X { get; set; }
    public int Y { get; set; }

    public RecordStructType()
    {
        X = 0;
        Y = 0;
    }

    public RecordStructType(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int Sum() => X + Y;
}
