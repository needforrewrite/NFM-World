using FixedMathSharp;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Tests that Fixed64? (nullable FixedMath types) are handled natively,
/// not wrapped in StructUserData (which would produce missing metatable errors).
/// </summary>
[LuaVisible]
public partial class TypeWithFixedMathNullables
{
    [LuaName] public Fixed64? NullableFixed { get; set; }
    [LuaName] public Vector3d? NullableVec3 { get; set; }

    [LuaName] public Fixed64 NormalFixed { get; set; }
    [LuaName] public Vector3d NormalVec3 { get; set; }

    [LuaName] public TypeWithFixedMathNullables() { }

    [LuaName] public Fixed64? GetOptionalValue(bool returnValue)
    {
        return returnValue ? NormalFixed : null;
    }
}
