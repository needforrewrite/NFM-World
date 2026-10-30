using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Rad;

[PluginVisible]
public enum LineType
{
    Flat, Charged, Colored, BrightColored,

    MaxValue = BrightColored
}