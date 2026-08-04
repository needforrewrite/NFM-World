using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures.Lua;

/// <summary>
/// Tests that types in namespaces containing "Lua" don't conflict with Lua.LuaFunction etc.
/// (Generated code should use global::Lua.ILuaUserData not Lua.ILuaUserData)
/// </summary>
[LuaVisible]
public partial class TypeInLuaNamespace
{
    public string Name { get; set; } = "";
    public int Value { get; set; }

    public TypeInLuaNamespace() { }

    public TypeInLuaNamespace(string name, int value)
    {
        Name = name;
        Value = value;
    }

    public string GetDescription() => $"{Name}:{Value}";
}
