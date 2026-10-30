using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[PluginVisible]
public enum PolyType
{
    // Put glass last so when rendering it is last in the render order due to alpha sorting
    Flat, Light, BrakeLight, ReverseLight, Finish, Fullbright,
    CGround, // SRC extension
    Glass,
    
    MaxValue = Glass
}