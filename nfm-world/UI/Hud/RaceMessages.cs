using System.Diagnostics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

/// <summary>
/// The two lines of race status text — a status line and a stunt line above it — drawn outside
/// the HUD skin so every skin gets them.
/// </summary>
public sealed class RaceMessages : Component
{
    /// <summary>Fraction of the viewport height each line starts at.</summary>
    private const float StuntTop = 0.22f;

    private const float StatusTop = 0.26f;

    /// <summary>Blink toggles per second, and how far the dark phase dims the colour.</summary>
    private const float BlinkHz = 20f;

    private const float BlinkDim = 0.75f;

    private readonly IHudData _race;
    private long _lastFrame = Stopwatch.GetTimestamp();
    private float _clock;

    public RaceMessages(IHudData race)
    {
        _race = race;
        // Absolute and full-screen for the same reason as RaceOverlays: as a flex child this
        // would be laid out in a row and get no usable bounds.
        Styles = Sx.Styles(
            position: Position.Absolute,
            left: 0,
            top: 0,
            width: 100.Percent(),
            height: 100.Percent(),
            flexShrink: 0,
            pointerEvents: false
        );
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        if (_race.IsPreview) return;
        var now = Stopwatch.GetTimestamp();
        var dt = Math.Clamp((float)Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds, 0, 0.1f);
        _lastFrame = now;
        if (!_race.Paused) _clock += dt;

        DrawLine(_race.StuntMessage, StuntTop, position, size);
        DrawLine(_race.StatusMessage, StatusTop, position, size);
    }

    private void DrawLine(HudMessage? message, float top, Vector2 position, Vector2 size)
    {
        if (message is not { } value) return;
        var scale = MathF.Min(size.X / 960, size.Y / 540);
        if (scale <= 0) return;

        var font = new Font(FontFamily.NotoSans, FontStyle.Bold, 16 * scale);
        G.SetFont(font);
        G.SetColor(ColourFor(value.Kind));
        // Area-relative draw: the height is the text's own line height, so `top` is the baseline.
        var lineHeight = (int)G.GetFontMetrics(font).LineHeight;
        G.DrawStringOutlinedAligned(
            value.Text,
            (int)position.X,
            (int)(position.Y + size.Y * top),
            (int)size.X,
            lineHeight,
            new Color(0, 0, 0),
            MathF.Max(1, scale),
            TextHorizontalAlignment.Center
        );
    }

    private Color ColourFor(HudMessageKind kind)
    {
        var colour = kind switch
        {
            HudMessageKind.Danger => Theme.Danger,
            HudMessageKind.Warning => Theme.Warning,
            _ => Theme.TextSecondary
        };
        if (!SettingsMenu.HudFlash) return colour;
        return (int)(_clock * BlinkHz) % 2 == 0
            ? colour
            : new Color((byte)(colour.R * BlinkDim), (byte)(colour.G * BlinkDim), (byte)(colour.B * BlinkDim), colour.A);
    }
}
