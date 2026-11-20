using System.Diagnostics;
using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorldLibrary;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>
/// The gadget skins' shared plumbing. phy's addon drew every gadget at fixed coordinates in an
/// 800×450 frame; each skin draws in those same coordinates and this class applies the origin
/// and the scale, so the ports read like the originals and stay vector-crisp at any resolution.
/// </summary>
internal abstract class GadgetHud(IHudData race) : Component
{
    protected readonly IHudData Race = race;

    private Vector2 _origin;
    internal float _scale;
    private long _lastFrame = Stopwatch.GetTimestamp();

    /// <summary>Seconds since mount, frozen while paused. Drives the damage blink and needle easing.</summary>
    protected float Clock { get; private set; }

    /// <summary>Seconds since the previous frame, capped so a hitch cannot fling the needles.</summary>
    protected float Dt { get; private set; }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var now = Stopwatch.GetTimestamp();
        var dt = Math.Clamp((float)Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds, 0, 0.1f);
        _lastFrame = now;
        Dt = dt;
        if (!Race.Paused) Clock += dt;

        var scale = MathF.Min(size.X / 800, size.Y / 450) * HudScaleSettings.Default * SettingsMenu.HudScale;
        if (scale <= 0) return;
        var bounds = HudSafeZone.Bounds(position, size);
        _boundsMin = new Vector2(bounds.X, bounds.Y);
        _boundsMax = new Vector2(bounds.X + bounds.Width, bounds.Y + bounds.Height);
        _scale = scale;
        Anchor(HudAnchor.TopLeft);

        G.SaveState();
        // phy drew on an aliased canvas, where a 1px line is one pixel wide; stay that thin.
        G.SetExactCoverage(true);
        try
        {
            DrawGadgets();
        }
        finally
        {
            G.SetExactCoverage(false);
            G.RestoreState();
        }
    }

    /// <summary>Draw the whole skin in phy's 800×450 frame, switching <see cref="Anchor"/> per gadget.</summary>
    protected abstract void DrawGadgets();

    protected enum HudAnchor
    {
        TopLeft,
        Top,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private Vector2 _boundsMin;
    private Vector2 _boundsMax;

    /// <summary>
    /// Pins the gadgets drawn next to a screen corner (or the top edge) of the safe zone. phy's
    /// 800×450 point at that corner lands on it, and the HUD scale grows away from it, so a
    /// right-hand gadget stays on the right edge instead of drifting off it.
    /// </summary>
    protected void Anchor(HudAnchor anchor)
    {
        var x = anchor switch
        {
            HudAnchor.TopLeft or HudAnchor.BottomLeft => _boundsMin.X,
            HudAnchor.Top => (_boundsMin.X + _boundsMax.X) / 2 - 400 * _scale,
            _ => _boundsMax.X - 800 * _scale
        };
        var y = anchor is HudAnchor.BottomLeft or HudAnchor.BottomRight
            ? _boundsMax.Y - 450 * _scale
            : _boundsMin.Y;
        _origin = new Vector2(x, y);
    }

    /// <summary>
    /// phy's `degreecnt` easing: the needle chases the target by a fraction of the gap each frame
    /// and snaps once it is within 1.5°.
    /// </summary>
    protected static float Ease(float eased, float target, float dt, float sensitivity = 3.55f)
    {
        var frames = MathF.Min(1f, dt * 60f);
        if (MathF.Abs(target - eased) < 1.5f * frames) return target;
        return eased + (target - eased) * frames / sensitivity;
    }

    // ── Coordinate transform: phy space → screen space ────────────────

    protected float S(float v) => v * _scale;
    protected float X(float x) => _origin.X + x * _scale;
    protected float Y(float y) => _origin.Y + y * _scale;

    /// <summary>phy's gauge-vertex math: `px - sin(deg)·r`, `py - cos(deg)·r`, in screen space.</summary>
    protected (float X, float Y) Polar(float px, float py, float degrees, float radius)
    {
        var rad = degrees * MathF.PI / 180f;
        return (px - MathF.Sin(rad) * radius, py - MathF.Cos(rad) * radius);
    }

    /// <summary>
    /// phy's gauge point as its integer canvas sees it: <c>px - (int)(sin·r·scale)</c>, truncating
    /// toward zero. The sine is phy's own table entry (a double rounded to float), because a float
    /// <c>MathF.Cos(120°)</c> lands a hair off −0.5 and truncates a pixel the other way.
    /// </summary>
    protected static (float X, float Y) Gauge(float px, float py, float degrees, float radius, float scale = 1f)
    {
        var rad = (int)degrees * (Math.PI / 180.0);
        var sin = (float)Math.Sin(rad);
        var cos = (float)Math.Cos(rad);
        return (px - (int)(sin * radius * scale), py - (int)(cos * radius * scale));
    }

    // ── Drawing helpers. All coordinates are phy's; transform happens here ──

    protected void Text(
        string text,
        float x,
        float y,
        float fontSize,
        Color color,
        FontFamily family = FontFamily.NotoSans,
        bool bold = true
    )
    {
        G.SetColor(color);
        G.SetFont(new Font(family, bold ? FontStyle.Bold : FontStyle.Plain, fontSize * _scale));
        // phy's drawString anchors at the baseline; this backend anchors at the line-box top, so
        // shift down by the gap between them.
        var metrics = G.GetFontMetrics();
        G.DrawString(text, (int)X(x), (int)(Y(y) + metrics.LineHeight - metrics.Ascent));
    }

    protected void TextRight(
        string text,
        float right,
        float y,
        float fontSize,
        Color color,
        FontFamily family = FontFamily.NotoSans,
        bool bold = true
    )
        => Text(text, right - TextWidth(text, fontSize, family, bold), y, fontSize, color, family, bold);

    protected void TextCenter(
        string text,
        float centerX,
        float y,
        float fontSize,
        Color color,
        FontFamily family = FontFamily.NotoSans,
        bool bold = true
    )
        => Text(text, centerX - TextWidth(text, fontSize, family, bold) / 2, y, fontSize, color, family, bold);

    protected float TextWidth(string text, float fontSize, FontFamily family = FontFamily.NotoSans, bool bold = true)
    {
        G.SetFont(new Font(family, bold ? FontStyle.Bold : FontStyle.Plain, fontSize * _scale));
        return G.GetFontMetrics().MeasureText(text).X / _scale;
    }

    protected void Polygon(Color color, ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        if (x.Length < 3 || x.Length != y.Length) return;
        Span<int> px = stackalloc int[x.Length];
        Span<int> py = stackalloc int[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            px[i] = (int)X(x[i]);
            py[i] = (int)Y(y[i]);
        }

        G.SetColor(color);
        G.FillPolygon(px, py, x.Length);
    }

    protected void PolygonOutline(Color color, float width, ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        if (x.Length < 3 || x.Length != y.Length) return;
        Span<int> px = stackalloc int[x.Length];
        Span<int> py = stackalloc int[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            px[i] = (int)X(x[i]);
            py[i] = (int)Y(y[i]);
        }

        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.DrawPolygon(px, py, x.Length);
    }

    protected void Line(Color color, float x1, float y1, float x2, float y2, float width = 1)
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.DrawLine((int)X(x1), (int)Y(y1), (int)X(x2), (int)Y(y2));
    }

    protected void Oval(Color color, float cx, float cy, float rx, float ry)
    {
        G.SetColor(color);
        G.BeginPath();
        G.Ellipse(X(cx), Y(cy), S(rx), S(ry));
        G.Fill();
    }

    protected void OvalOutline(Color color, float width, float cx, float cy, float rx, float ry)
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.BeginPath();
        G.Ellipse(X(cx), Y(cy), S(rx), S(ry));
        G.Stroke();
    }

    /// <summary>
    /// Java's arc parameterization, emitted as a polyline: <c>cx + r·cos θ</c>, <c>cy − r·sin θ</c>
    /// with θ in degrees and positive counter-clockwise. G.Arc's flag handling diverges from
    /// Java2D on sweeps wider than a half turn, so the wedge is walked here instead — a 3° step is
    /// smooth at HUD sizes and cannot disagree with the reference.
    /// </summary>
    private void ArcPath(
        float cx,
        float cy,
        float rx,
        float ry,
        float startDeg,
        float sweepDeg,
        bool toCentre
    )
    {
        var segments = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(sweepDeg) / 3f));
        var dx = S(rx);
        var dy = S(ry);
        var ox = X(cx);
        var oy = Y(cy);

        static (float X, float Y) At(float ox, float oy, float rx, float ry, float deg)
        {
            var rad = deg * MathF.PI / 180f;
            return (ox + rx * MathF.Cos(rad), oy - ry * MathF.Sin(rad));
        }

        var start = At(ox, oy, dx, dy, startDeg);
        if (toCentre)
        {
            G.MoveTo(ox, oy);
            G.LineTo(start.X, start.Y);
        }
        else
        {
            G.MoveTo(start.X, start.Y);
        }

        for (var i = 1; i <= segments; i++)
        {
            var p = At(ox, oy, dx, dy, startDeg + sweepDeg * i / segments);
            G.LineTo(p.X, p.Y);
        }

        if (toCentre) G.LineTo(ox, oy);
    }

    /// <summary>phy's `drawArc(x, y, w, h, start, sweep)`: a stroked arc.</summary>
    protected void ArcStroke(
        Color color,
        float width,
        float cx,
        float cy,
        float rx,
        float ry,
        float startDeg,
        float sweepDeg
    )
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.BeginPath();
        ArcPath(cx, cy, rx, ry, startDeg, sweepDeg, false);
        G.Stroke();
    }

    /// <summary>phy's `fillArc(x, y, w, h, start, sweep)`: a filled pie wedge.</summary>
    protected void ArcFill(Color color, float cx, float cy, float rx, float ry, float startDeg, float sweepDeg)
    {
        G.SetColor(color);
        G.BeginPath();
        ArcPath(cx, cy, rx, ry, startDeg, sweepDeg, true);
        G.ClosePath();
        G.Fill();
    }

    /// <summary>A region filled as one outline, so overlapping pieces blend once.</summary>
    protected void FillRegion(Color color, ShapeRegion region)
    {
        G.SetColor(color);
        G.BeginPath();
        AddContours(region);
        G.Fill();
    }

    protected void StrokeRegion(Color color, float width, ShapeRegion region)
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.BeginPath();
        AddContours(region);
        G.Stroke();
    }

    private void AddContours(ShapeRegion region)
    {
        foreach (var contour in region.Contours)
        {
            if (contour.Length < 3) continue;
            G.MoveTo(X(contour[0].X), Y(contour[0].Y));
            for (var i = 1; i < contour.Length; i++)
                G.LineTo(X(contour[i].X), Y(contour[i].Y));
            G.ClosePath();
        }
    }

    protected void RoundedRect(Color color, float x, float y, float w, float h, float radius)
    {
        G.SetColor(color);
        G.FillRoundedRect((int)X(x), (int)Y(y), (int)S(w), (int)S(h), S(radius), S(radius), S(radius), S(radius));
    }

    protected void RoundedRectOutline(Color color, float width, float x, float y, float w, float h, float radius)
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.DrawRoundedRect((int)X(x), (int)Y(y), (int)S(w), (int)S(h), S(radius), S(radius), S(radius), S(radius));
    }

    protected void Rect(Color color, float x, float y, float w, float h)
    {
        G.SetColor(color);
        G.FillRect((int)X(x), (int)Y(y), (int)S(w), (int)S(h));
    }

    protected void RectOutline(Color color, float width, float x, float y, float w, float h)
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.DrawRect((int)X(x), (int)Y(y), (int)S(w), (int)S(h));
    }

    /// <summary>Clip further drawing to a phy-space (optionally rounded) rectangle.</summary>
    protected void ClipRect(float x, float y, float w, float h, float radius = 0)
        => G.IntersectScissor(X(x), Y(y), S(w), S(h), S(radius));

    // ── Shared gadget math ────────────────────────────────────────────

    /// <summary>
    /// phy's damage-colour ramp: green falling to red as damage rises, with the high-damage blink.
    /// </summary>
    protected Color DamageColor(float damage, bool blink = true, int alpha = 255)
    {
        var amount = (int)(98 * Math.Clamp(damage, 0, 1));
        var green = amount > 33 ? (int)(244 - 233f * (amount - 33) / 65) : 244;
        if (blink && amount > 70 && SettingsMenu.HudFlash)
        {
            var period = (168 - amount * 1.5f) / 30;
            if (Clock % period > period / 2) green = 170;
        }

        return new Color(244, Math.Clamp(green, 0, 255), 11, alpha);
    }

    // ── Screen-space helpers for the radar, which works in already-scaled pixels ──

    /// <summary>Screen-space line, for the radar whose points are already transformed.</summary>
    internal void ScreenLine(Color color, Vector2 a, Vector2 b, float width = 1)
    {
        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.DrawLine((int)a.X, (int)a.Y, (int)b.X, (int)b.Y);
    }

    internal void ScreenOval(Color color, Vector2 center, float radius)
    {
        G.SetColor(color);
        G.BeginPath();
        G.Ellipse(center.X, center.Y, radius * _scale, radius * _scale);
        G.Fill();
    }

    /// <summary>phy's drawPlayerIcon style 1: a four-point chevron with the nose up.</summary>
    internal void ScreenIcon(Vector2 center, Color fill, Color outline)
    {
        Span<int> xs = stackalloc int[4];
        Span<int> ys = stackalloc int[4];
        xs[0] = (int)center.X;
        ys[0] = (int)(center.Y - 6 * _scale);
        xs[1] = (int)(center.X - 5 * _scale);
        ys[1] = (int)(center.Y + 6 * _scale);
        xs[2] = (int)center.X;
        ys[2] = (int)(center.Y + 4 * _scale);
        xs[3] = (int)(center.X + 5 * _scale);
        ys[3] = (int)(center.Y + 6 * _scale);
        G.SetColor(fill);
        G.FillPolygon(xs, ys, 4);
        G.SetColor(outline);
        G.SetStrokeWidth(MathF.Max(1, _scale));
        G.DrawPolygon(xs, ys, 4);
    }

    /// <summary>Screen-space polygon, for gauge vertices already transformed by <see cref="Polar"/>.</summary>
    protected void ScreenPolygon(Color color, ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        if (x.Length < 3 || x.Length != y.Length) return;
        Span<int> px = stackalloc int[x.Length];
        Span<int> py = stackalloc int[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            px[i] = (int)x[i];
            py[i] = (int)y[i];
        }

        G.SetColor(color);
        G.FillPolygon(px, py, x.Length);
    }

    /// <summary>Screen-space polygon outline, for the arrow's lit border.</summary>
    protected void ScreenPolygonOutline(Color color, float width, ReadOnlySpan<float> x, ReadOnlySpan<float> y)
    {
        if (x.Length < 3 || x.Length != y.Length) return;
        Span<int> px = stackalloc int[x.Length];
        Span<int> py = stackalloc int[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            px[i] = (int)x[i];
            py[i] = (int)y[i];
        }

        G.SetColor(color);
        G.SetStrokeWidth(MathF.Max(1, width * _scale));
        G.DrawPolygon(px, py, x.Length);
    }

    /// <summary>Draws the radar's baked stage image as the parallelogram the placement describes.</summary>
    internal void DrawImage(RadarMapPlacement placement, IImage image)
        => G.DrawImageTransformed(image, placement.Origin, placement.AxisX, placement.AxisY, new Color(255, 255, 255, 220));


    internal static Color Snap(Color color) => HudColorSnap.Apply(color, World.Snap);

    internal static Color Snap(int r, int g, int b) => Snap(new Color(r, g, b));

    internal static Color Snap(int r, int g, int b, int a) => Snap(new Color(r, g, b, a));

    protected static string Ordinal(int value) => $"{value}{OrdinalSuffix(value)}";

    protected static string OrdinalSuffix(int value) => value % 100 is 11 or 12 or 13
        ? "th"
        : (value % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

    /// <summary>phy's Chronometer.getTime: mm:ss.cc.</summary>
    protected static string Time(int ms)
    {
        ms = Math.Max(0, ms);
        return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:00}";
    }
}
