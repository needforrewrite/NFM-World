using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

public static class Physics
{
    public const float OriginalTps = 21.4f;
    public const float TargetTps = 63f;
    public const int OriginalTicksPerNewTick = 3;
    public const float PHYSICS_MULTIPLIER = OriginalTps/TargetTps;
    public static fix64 PHYSICS_MULTIPLIER_F64 { get; } = (fix64)(PHYSICS_MULTIPLIER);

}