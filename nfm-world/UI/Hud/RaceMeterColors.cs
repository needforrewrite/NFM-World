namespace NFMWorld.UI.Hud;

internal static class RaceMeterColors
{
    public static Color Power(float power)
        => new(power >= 1 ? 64 : 128, (int)(190 + power * 98 * 0.37f), 244);

    public static Color Damage(float damage, bool highlighted = false)
    {
        var amount = (int)(98 * damage);
        var green = highlighted ? 170 : amount > 33 ? (int)(244 - 233f * (amount - 33) / 65) : 244;
        return new Color(244, green, 11);
    }
}
