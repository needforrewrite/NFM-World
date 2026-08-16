using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that const fields are treated as read-only (not writable from Lua).
/// </summary>
[LuaVisible]
public static partial class TypeWithConstants
{
    [LuaName]
    public const int Factor = 100;
    [LuaName]
    public const string DefaultName = "Default";
    [LuaName]
    public const double Pi = 3.14159;

    [LuaName]
    public static int Multiplier = 1; // writable

    [LuaName]
    public static int ApplyFactor(int value) => value * Factor;
}
