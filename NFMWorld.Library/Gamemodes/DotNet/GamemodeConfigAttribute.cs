using System.Diagnostics.CodeAnalysis;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes.DotNet;

// These three are only ever reached through reflection, from an assembly loaded at run time, so a
// trimmed build has no static evidence that anything uses them. Without the annotation the trimmer
// removes their constructors and properties and the plugin fails with "Method not found".

[AttributeUsage(AttributeTargets.Assembly)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[PluginVisible(Visible.IncludingAllMembersOfType)]
public class GamemodeAttribute : Attribute
{
    public required string Name { get; set; }
    public required string Description { get; set; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[PluginVisible(Visible.IncludingAllMembersOfType)]
public class GamemodeConfigPropertyAttribute : Attribute
{
    public required string Name { get; set; }
    public required Type Type { get; set; }
    public required string Label { get; set; }
    public required string Description { get; set; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[PluginVisible(Visible.IncludingAllMembersOfType)]
public class GamemodeConfigPropertyOptionAttribute(string property) : Attribute
{
    public string Property { get; } = property;
    public required string Label { get; set; }
    public required string Value { get; set; }
}
