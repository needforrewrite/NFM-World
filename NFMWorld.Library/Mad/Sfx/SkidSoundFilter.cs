namespace NFMWorldLibrary;

public static class SkidSoundFilter
{
    private const float MinimumLateralSpeed = 15;
    private const float MinimumSlipRatio = 0.12f;

    public static bool HasLateralSlip(float velocityX, float velocityZ, float headingDegrees)
        => HasSlip(velocityX, velocityZ, headingDegrees, MinimumLateralSpeed, MinimumSlipRatio);

    public static bool HasDriftSlip(float velocityX, float velocityZ, float headingDegrees)
        => HasSlip(velocityX, velocityZ, headingDegrees, 25, 0.35f);

    private static bool HasSlip(
        float velocityX,
        float velocityZ,
        float headingDegrees,
        float minimumLateralSpeed,
        float minimumSlipRatio
    )
    {
        if (!float.IsFinite(velocityX) || !float.IsFinite(velocityZ) || !float.IsFinite(headingDegrees)) return false;
        var speed = MathF.Sqrt(velocityX * velocityX + velocityZ * velocityZ);
        if (!float.IsFinite(speed) || speed < minimumLateralSpeed) return false;
        var (sin, cos) = MathF.SinCos(headingDegrees * MathF.PI / 180);
        var lateralSpeed = MathF.Abs(velocityX * cos + velocityZ * sin);
        return lateralSpeed >= minimumLateralSpeed && lateralSpeed >= speed * minimumSlipRatio;
    }
}
