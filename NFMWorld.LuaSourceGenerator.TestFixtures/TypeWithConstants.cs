using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that const fields are treated as read-only (not writable from Lua).
/// </summary>
[LuaVisible]
public static partial class TypeWithConstants
{
    public const int Factor = 100;
    public const string DefaultName = "Default";
    public const double Pi = 3.14159;

    public static int Multiplier = 1; // writable

    public static int ApplyFactor(int value) => value * Factor;
}
