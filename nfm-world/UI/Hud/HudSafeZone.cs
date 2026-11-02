using NFMWorld.Reactor;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

internal static class HudSafeZone
{
    public static RectangleF Bounds(Vector2 position, Vector2 size) => HudSafeZoneSettings.Bounds(position, size,
        SettingsMenu.HudSafeZoneHorizontal, SettingsMenu.HudSafeZoneVertical);
}

internal sealed class HudSafeZoneGuide : Component
{
    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var bounds = HudSafeZone.Bounds(position, size);
        G.SetColor(new Color(128, 170, 255, 150));
        G.SetStrokeWidth(1);
        G.DrawRect((int)bounds.X, (int)bounds.Y, Math.Max(0, (int)bounds.Width - 1), Math.Max(0, (int)bounds.Height - 1));
    }
}
