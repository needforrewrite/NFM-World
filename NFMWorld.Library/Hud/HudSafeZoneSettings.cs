using System.Globalization;

namespace NFMWorldLibrary.Hud;

public static class HudSafeZoneSettings
{
    public const float Default = 1;
    public const float Minimum = 0.8f;
    public const float Maximum = 1;

    public static float Normalize(float value)
        => float.IsFinite(value) ? Math.Clamp(value, Minimum, Maximum) : Default;

    public static float Parse(string value)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio)
            ? Normalize(ratio) : Default;

    public static RectangleF Bounds(Vector2 position, Vector2 size, float horizontal, float vertical)
    {
        var width = size.X * Normalize(horizontal);
        var height = size.Y * Normalize(vertical);
        return new RectangleF(position.X + (size.X - width) / 2, position.Y + (size.Y - height) / 2, width, height);
    }
}
