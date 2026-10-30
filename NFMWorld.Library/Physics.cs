using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

[PluginVisible]
public static class Physics
{
    [PluginVisible] public const float OriginalTps = 21.4f;
    [PluginVisible] public const float TargetTps = 63f;
    [PluginVisible] public const int OriginalTicksPerNewTick = 3;
    [PluginVisible] public const float PHYSICS_MULTIPLIER = OriginalTps/TargetTps;
    [PluginVisible] public static fix64 PHYSICS_MULTIPLIER_F64 { get; } = (fix64)(PHYSICS_MULTIPLIER);

}