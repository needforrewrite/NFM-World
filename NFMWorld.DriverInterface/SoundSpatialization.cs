namespace NFMWorld.DriverInterface.DriverInterface;

public static class SoundSpatialization
{
    public const float ReferenceDistance = 1200;
    public const float FadeDistance = 12000;
    public const float MaximumDistance = 18000;

    public static float DistanceGain(float distance)
    {
        if (!float.IsFinite(distance) || distance >= MaximumDistance) return 0;
        if (distance <= ReferenceDistance) return 1;
        var gain = ReferenceDistance / distance;
        if (distance > FadeDistance)
            gain *= (MaximumDistance - distance) / (MaximumDistance - FadeDistance);
        return gain;
    }
}
