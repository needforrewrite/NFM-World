using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Exercises member-level [LuaShimType] overrides on parameters, fields,
/// properties, and return values for generated LuaLS shims.
/// </summary>
[LuaVisible]
public partial class TypeWithMemberShimOverrides
{
    [LuaShimType("CustomField")]
    [LuaName]
    public string MyField = "";

    [LuaShimType("CustomProperty")]
    [LuaName]
    public string MyProperty { get; set; } = "";

    [LuaName]
    public int MethodWithParamShim([LuaShimType("CustomParam")] int value) => value;

    [LuaName]
    [return: LuaShimType("CustomReturn")]
    public int MethodWithReturnShim() => 42;
}
