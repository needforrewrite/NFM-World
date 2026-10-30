using System.Diagnostics.CodeAnalysis;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Plugins;

// Only ever reached through reflection, from an assembly loaded at run time, so a trimmed
// build has no static evidence that anything uses it. Without the annotation the trimmer
// removes its constructor and properties and the plugin fails with "Method not found".

[AttributeUsage(AttributeTargets.Assembly)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[PluginVisible(Visible.IncludingAllMembersOfType)]
public class AiAttribute : Attribute
{
    public required string Name { get; set; }
    public string Description { get; set; } = "";
}
