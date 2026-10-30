
namespace NFMWorld.UI;

/// <summary>
/// The UI colour palette. Port of <c>data/library/ui/theme.luau</c>; keep the two in
/// step until the Luau copy is deleted.
/// </summary>
public static class Theme
{
    // Accent (primary)
    public static readonly Color Accent = FromHex(0x4fc3f7);
    public static readonly Color AccentHover = FromHex(0x7dd8f9);
    public static readonly Color AccentSoft = new(79, 195, 247, 46);
    public static readonly Color AccentSoftStrong = new(79, 195, 247, 82);
    public static readonly Color AccentBorder = new(79, 195, 247, 89);

    // Danger
    public static readonly Color Danger = FromHex(0xff5252);
    public static readonly Color DangerSoft = new(255, 82, 82, 38);
    public static readonly Color DangerSoftStrong = new(255, 82, 82, 71);
    public static readonly Color DangerBorder = new(255, 82, 82, 89);

    // Warning (orange)
    public static readonly Color Warning = FromHex(0xffb300);
    public static readonly Color WarningSoft = new(255, 152, 0, 64);
    public static readonly Color WarningSoftStrong = new(255, 152, 0, 89);
    public static readonly Color WarningBorder = new(255, 152, 0, 128);

    // Success
    public static readonly Color Success = FromHex(0x69f0ae);

    // Text
    public static readonly Color TextPrimary = new(255, 255, 255);
    public static readonly Color TextSecondary = new(255, 255, 255, 204);
    public static readonly Color TextMuted = new(255, 255, 255, 128);
    public static readonly Color TextFaint = new(255, 255, 255, 89);

    // Surfaces
    public static readonly Color Surface = new(255, 255, 255, 15);
    public static readonly Color SurfaceHover = new(255, 255, 255, 31);
    public static readonly Color SurfaceBorder = new(255, 255, 255, 31);

    // Panels / overlays
    public static readonly Color Panel = FromHex(0x1a1a2e);
    public static readonly Color Overlay = new(0, 0, 0, 166);

    public static readonly Color Transparent = new(0, 0, 0, 0);

    private static Color FromHex(int rgb)
        => new((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
