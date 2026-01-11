using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

/// <summary>
/// Test ref struct - should be completely excluded from generation
/// Ref structs cannot be marshalled to Lua
/// </summary>
[LuaVisible]
public ref struct RefStructType
{
    public int X;
    public int Y;

    public RefStructType(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int GetSum()
    {
        return X + Y;
    }
}
