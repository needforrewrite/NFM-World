namespace NFMWorldLibrary.Hud;

public static class HudColorSnap
{
    public static Color Apply(Color color, Color3 snap) => new(
        Math.Clamp((int)(color.R * (1 + snap.R / 100f)), 0, 255),
        Math.Clamp((int)(color.G * (1 + snap.G / 100f)), 0, 255),
        Math.Clamp((int)(color.B * (1 + snap.B / 100f)), 0, 255), color.A);
}
