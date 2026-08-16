using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures.Lua;

/// <summary>
/// Tests that types in namespaces containing "Lua" don't conflict with Lua.LuaFunction etc.
/// (Generated code should use global::Lua.ILuaUserData not Lua.ILuaUserData)
/// </summary>
[LuaVisible]
public partial class TypeInLuaNamespace
{
    [LuaName] public string Name { get; set; } = "";
    [LuaName] public int Value { get; set; }

    [LuaName] public TypeInLuaNamespace() { }

    [LuaName] public TypeInLuaNamespace(string name, int value)
    {
        Name = name;
        Value = value;
    }

    [LuaName] public string GetDescription() => $"{Name}:{Value}";
}
