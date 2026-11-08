using System.Globalization;

namespace NFMWorldLibrary.Hud;

public static class HudScaleSettings
{
    public const float Default = 1;
    public const float Minimum = 0.5f;
    public const float Maximum = 2;
    public const float ClassicDefault = 1.15f;

    public static float Normalize(float value)
        => float.IsFinite(value) ? Math.Clamp(value, Minimum, Maximum) : Default;

    public static float Parse(string value)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            ? Normalize(scale)
            : Default;
}
