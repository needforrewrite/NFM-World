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
    public Fixed64? NullableFixed { get; set; }
    public Vector3d? NullableVec3 { get; set; }

    public Fixed64 NormalFixed { get; set; }
    public Vector3d NormalVec3 { get; set; }

    public TypeWithFixedMathNullables() { }

    public Fixed64? GetOptionalValue(bool returnValue)
    {
        return returnValue ? NormalFixed : null;
    }
}
