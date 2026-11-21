using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>
/// S7, phy's fourth gadget skin: skewed trapezoid panels, the Adventure font, the 240°
/// rainbow-rimmed speedometer with colour-shifted tick labels and the wide chevron arrow.
/// </summary>
internal sealed class S7Hud(IHudData race) : GadgetHud(race)
{
    protected override void DrawGadgets()
    {
        Anchor(HudAnchor.TopLeft);
        Position();
        Anchor(HudAnchor.TopRight);
        Status();
        if (!Race.TimeTrial && Race.Racers.Count > 1) Opponents();
        if (Race.Opponents == 0 && Race.LapSplits.Count > 1) Chronometer();
        Anchor(HudAnchor.Top);
        Arrow();
        Anchor(HudAnchor.BottomLeft);
        Radar();
        if (Race.MusicTrack is { } track) TrackVisor(track);
        Anchor(HudAnchor.BottomRight);
        Speedometer();
    }

    // ── Position. GadgetPainter.position, poson 4 ─────────────────────

    private void Position()
    {
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250);

        // phy's skewed trapezoid panel.
        Span<float> xs = [-1, 220, 200, -1];
        Span<float> ys = [6, 6, 56, 56];
        Polygon(Snap(50, 50, 50, 220), xs, ys);
        PolygonOutline(accent, 1, xs, ys);

        Text("Wasted:", 102, 22, 14, white, FontFamily.Adventure, false);
        Text(Race.HasPosition ? "Position:" : "Pos:", 32, 42, 14, white, FontFamily.Adventure, false);

        var blue = Snap(50, 204, 255);
        if (Race.TotalLaps > 0)
        {
            Text("Lap:", 15, 22, 14, white, FontFamily.Adventure, false);
            Text($"{Race.Lap} / {Race.TotalLaps}", 46, 22, 14, blue, FontFamily.Adventure, false);
        }

        if (!Race.TimeTrial)
            Text($"{Race.Wasted} / {Race.Opponents}", 160, 22, 14, blue, FontFamily.Adventure, false);

        if (!Race.HasPosition) return;
        var pos = Race.Position;
        var alive = Race.TotalRacers - Race.Wasted;
        var suffix = $"{OrdinalSuffix(pos)} / {alive}";
        Text(suffix, 125, 42, 14, blue, FontFamily.Adventure, false);
        TextRight($"{pos}", 121, 52, 32, white, FontFamily.Adventure, false);
    }

    // ── Damage / power. GadgetPainter.status, staton 4 ───────────────

    private void Status()
    {
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250);

        // The skewed panel, mirrored to the top-right.
        Span<float> xs = [580, 801, 801, 600];
        Span<float> ys = [6, 6, 56, 56];
        Polygon(Snap(50, 50, 50, 220), xs, ys);
        PolygonOutline(accent, 1, xs, ys);

        Text("Damage", 605, 27, 14, white, FontFamily.Adventure, false);
        Text("Power", 614, 47, 14, white, FontFamily.Adventure, false);

        // Damage: the clipped parallelogram bar on the red ramp, with the shake when it moved.
        var damage = Math.Clamp(Race.DamageFraction, 0, 1);
        var dam = damage * 116;
        var damagePercent = (int)(damage * 100);
        var barColor = dam > 90 ? Snap(255, 0, 0) : dam > 50 ? Snap(250, 140, 0) : Snap(255, 255, 0);
        if (dam > 0)
        {
            ClipRect(669, 14, 116, 14);
            Span<float> barXs = [650, 674 + dam, 669 + dam, 650];
            Span<float> barYs = [14, 14, 28, 28];
            Polygon(barColor, barXs, barYs);
            G.ResetScissor();
        }

        if (dam > 30)
            TextCenter($"{damagePercent}%", 669 + dam / 2, 25, 11, Snap(0, 0, 0));
        else
            Text($"{damagePercent}%", 675 + dam, 25, 11, white);

        // Power: the ticked bar with the travelling highlight, in the skin's cyan.
        var power = Math.Clamp(Race.PowerFraction, 0, 1);
        var pow = power * 116;
        var glowTick = (int)(Clock * 20) % 114;
        for (var i = 0; i + 6 <= pow && i < 114; i += 6)
            Rect(Snap(50, 204, 255, 220), 671 + i, 36, 4, 10);
        if (power >= 0.95f && glowTick < pow)
            Rect(Snap(50, 204, 255, 220), 671 + glowTick, 35, 4, 12);
        TextRight($"{(int)(power * 100)}%", 782, 45, 11, white);

        // Frames: black inner, red for damage, cyan for power.
        var frame = Snap(0, 0, 0);
        RectOutline(frame, 2, 668, 13, 118, 16);
        RectOutline(frame, 2, 668, 33, 118, 16);
        RectOutline(Snap(255, 0, 0), 1, 667, 12, 119, 17);
        RectOutline(Snap(50, 204, 255), 1, 667, 32, 119, 17);
    }

    // ── Opponents. GadgetPainter.opponentStatus, opstaton 4 ──────────

    private void Opponents()
    {
        const int maxRows = 11;
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250);
        var row = 0;
        foreach (var racer in Race.Racers)
        {
            if (racer.Wasted) continue;
            if (row >= maxRows) break;
            var y = 60 + 30 * row;
            Span<float> xs = [682, 793, 787, 676];
            Span<float> ys = [y, y, y + 25, y + 25];
            Polygon(Snap(50, 50, 50, 220), xs, ys);

            TextRight($"{row + 1}", 768, y + 22, 23, white, FontFamily.Adventure, false);

            var damagePercent = (int)(Math.Clamp(racer.Damage, 0, 1) * 100);
            var color = damagePercent >= 80 ? Snap(250, 30, 0, 220)
                : damagePercent >= 60 ? Snap(250, 140, 0, 220)
                : damagePercent >= 25 ? Snap(250, 250, 0, 220)
                : Snap(50, 250, 0, 220);
            var lit = Math.Clamp(racer.Damage, 0, 1) * 60;
            Span<float> barXs = [694, 694 + lit, 692 + lit, 692];
            Span<float> barYs = [y + 16, y + 16, y + 21, y + 21];
            Polygon(color, barXs, barYs);
            PolygonOutline(Snap(250, 250, 250, 120), 1, [693, 753, 751, 691], [y + 15, y + 15, y + 21, y + 21]);
            TextCenter(racer.Name, 723, y + 12, 11, white);
            row++;
        }
    }

    // ── Guidance arrow. GadgetPainter.arrow, arrowon 4: the wide chevron ──

    private void Arrow()
    {
        if (Race.ArrowAngle is not { } angle) return;
        var white = Snap(250, 250, 250);
        var accent = Race.ArrowAtCars ? Snap(50, 204, 255) : Snap(80, 230, 30);
        var dark = Race.ArrowAtCars ? Snap(0, 154, 205, 220) : Snap(50, 150, 20, 220);

        // The trapezoid banner.
        Span<float> bannerXs = [320, 340, 460, 479];
        Span<float> bannerYs = [-1, 30, 30, -1];
        Polygon(Snap(50, 50, 50, 220), bannerXs, bannerYs);

        var name = Race.ArrowTarget;
        TextCenter(name, 400, 13, 12, white);
        TextCenter(Race.ArrowTarget, 400, 24, 10, Snap(220, 220, 220), FontFamily.DroidSans, false);

        // The chevron: seven vertices sweeping around the heading.
        var px = X(400);
        var py = Y(51);
        Span<float> xs =
        [
            Polar(px, py, angle - 180, S(19)).X,
            Polar(px, py, angle + 90, S(16)).X,
            Polar(px, py, angle + 90, S(5)).X,
            Polar(px, py, angle + 24, S(20)).X,
            Polar(px, py, angle - 24, S(20)).X,
            Polar(px, py, angle - 90, S(5)).X,
            Polar(px, py, angle - 90, S(16)).X
        ];
        Span<float> ys =
        [
            Polar(px, py, angle - 180, S(19)).Y,
            Polar(px, py, angle + 90, S(16)).Y,
            Polar(px, py, angle + 90, S(5)).Y,
            Polar(px, py, angle + 24, S(20)).Y,
            Polar(px, py, angle - 24, S(20)).Y,
            Polar(px, py, angle - 90, S(5)).Y,
            Polar(px, py, angle - 90, S(16)).Y
        ];

        // Shadow pass then the lit fill and outline. phy translated the shadow by (0, 5).
        G.SaveState();
        G.IntersectScissor(X(320), Y(-4), S(160), S(80));
        ScreenPolygon(Snap(50, 50, 50, 220), xs, Shift(ys, 5 * _scale));
        ScreenPolygon(accent, xs, ys);
        ScreenPolygonOutline(dark, 2, xs, ys);
        G.RestoreState();
    }

    private static float[] Shift(ReadOnlySpan<float> values, float delta)
    {
        var shifted = new float[values.Length];
        for (var i = 0; i < values.Length; i++)
            shifted[i] = values[i] + delta;
        return shifted;
    }

    // ── Radar. GadgetPainter.radarstat, radaron 4 ─────────────────────

    private void Radar()
    {
        if (Race.ClientPosition is null) return;
        var side = S(172);
        var x = X(6);
        var y = Y(290);
        Rect(Snap(50, 50, 50, 220), 6, 290, 172, 120);
        ClipRect(6, 290, 172, 120);
        GadgetRadar.Draw(this, Race, new Vector2(x + side / 2, y + S(120) / 2), side);
        G.ResetScissor();
        RectOutline(Snap(50, 204, 255, 220), 1, 6, 290, 172, 120);
    }

    // ── Chronometer. Chronometer.paint, style 4 ──────────────────────

    private void Chronometer()
    {
        var splits = Race.LapSplits;
        var rows = Math.Clamp(splits.Count, 1, 10);
        var h = 20 * (rows + 1);
        RoundedRect(Snap(50, 50, 50, 220), 602, 65, 177, h, 7);

        var white = Snap(250, 250, 250);
        var accent = Snap(50, 204, 255, 220);
        TextCenter("# Lap", 624, 80, 11, accent, FontFamily.Adventure, false);
        TextCenter("Lap Time", 679, 80, 11, accent, FontFamily.Adventure, false);
        TextCenter("Split Time", 746, 80, 11, accent, FontFamily.Adventure, false);
        Line(Snap(50, 50, 50), 645, 65, 645, 65 + h - 1);
        Line(Snap(50, 50, 50), 713, 65, 713, 65 + h - 1);

        var cumulative = 0;
        for (var i = 0; i < rows; i++)
        {
            cumulative += splits[i];
            var y = 65 + 20 * (i + 1);
            Line(Snap(0, 0, 0, 110), 602, y, 779, y);
            TextCenter($"{i + 1}", 624, y + 15, 11, white);
            TextCenter(Time(splits[i]), 679, y + 15, 11, white);
            TextCenter(Time(cumulative), 746, y + 15, 11, white);
        }
    }

    // ── Speedometer. GadgetPainter.speedometer, speedon 4, ported line for line ──

    private const int SpeedoX = 730;
    private const int SpeedoY = 365;
    private const float SpeedoScale = 0.8f;

    // The bezel's skirt, and the dial + triangle + skirt filled as a single translucent shape.
    private static readonly (float[] X, float[] Y) Skirt = BuildSkirt();

    private static readonly ShapeRegion Shell = BuildShell();

    private static (float[] X, float[] Y) BuildSkirt()
    {
        var l = Gauge(SpeedoX, SpeedoY, -120, 80, SpeedoScale);
        var r = Gauge(SpeedoX, SpeedoY, 120, 80, SpeedoScale);
        const int b = 3;
        return (
            [l.X + b, l.X + b, l.X + b - 3, r.X - b + 3, r.X - b, r.X - b],
            [l.Y, l.Y + 14, l.Y + 17, r.Y + 17, r.Y + 14, r.Y]
        );
    }

    private static ShapeRegion BuildShell()
    {
        var l = Gauge(SpeedoX, SpeedoY, -120, 80, SpeedoScale);
        var r = Gauge(SpeedoX, SpeedoY, 120, 80, SpeedoScale);
        var skirt = new float[Skirt.X.Length * 2];
        for (var i = 0; i < Skirt.X.Length; i++)
        {
            skirt[i * 2] = Skirt.X[i];
            skirt[i * 2 + 1] = Skirt.Y[i];
        }

        return new ShapeRegion()
            .Add(ShapeRegion.Pie(SpeedoX, SpeedoY, 64, 64, -30, 240))
            .Add(ShapeRegion.Polygon(SpeedoX, SpeedoY, l.X - 1, l.Y - 1, r.X + 1, r.Y - 1))
            .Add(ShapeRegion.Polygon(skirt));
    }

    // phy's label colours: [passed by the needle, still ahead], one pair per 30° step from -120°.
    private static readonly ((int R, int G, int B) Passed, (int R, int G, int B) Ahead)[] LabelColors =
    [
        ((70, 0, 0), (170, 30, 30)),
        ((150, 0, 0), (230, 30, 30)),
        ((150, 30, 0), (230, 60, 30)),
        ((150, 70, 0), (230, 100, 30)),
        ((150, 150, 30), (230, 230, 60)),
        ((130, 150, 45), (190, 230, 80)),
        ((100, 150, 45), (150, 230, 80)),
        ((50, 150, 20), (80, 230, 30)),
        ((0, 80, 0), (30, 150, 30))
    ];

    private static readonly string[] Labels = ["0", "45", "90", "135", "180", "225", "270", "315", "360"];

    private void Speedometer()
    {
        const int px = SpeedoX;
        const int py = SpeedoY;
        const float scl = SpeedoScale;
        const int mins = -120;
        const int maxs = 120;
        var accent = Snap(50, 204, 255, 220);

        FillRegion(Snap(50, 50, 50, 220), Shell);
        PolygonOutline(accent, 1, Skirt.X, Skirt.Y);
        ArcStroke(accent, 1, px, py, 64, 64, -15, 210);

        // phy's needle angle is in raw speed units: 120 − |speed|·2/3, clamped to ±120.
        var rawSpeed = MathF.Abs(Race.SpeedFraction ?? 0) * (Race.TopSpeed ?? 0);
        var degrees = Math.Clamp(120 - (int)(rawSpeed * 2f / 3f), mins, maxs);

        if (Race.SpeedFraction is < 0)
            Text("[Rev]", 717, 386, 11, Snap(100, 140, 10));
        if (Race.SpeedFraction >= 1)
            Text("[Max]", 717, 386, 11, Snap(255, 0, 0));

        // The needle chases the target, and a preview shows the target itself.
        _needle = Race.IsPreview ? degrees : Ease(_needle, degrees, Dt);

        // Ticks: long every 30°, two short between.
        for (var n = mins; n <= maxs; n += 30)
        {
            var a = Gauge(px, py, n, 70, scl);
            var c = Gauge(px, py, n, 80, scl);
            Line(accent, a.X, a.Y, c.X, c.Y);
        }

        for (var offset = 10; offset <= 20; offset += 10)
        for (var n = mins + offset; n <= maxs; n += 30)
        {
            var a = Gauge(px, py, n, 80, scl);
            var c = Gauge(px, py, n, 75, scl);
            Line(accent, a.X, a.Y, c.X, c.Y);
        }

        // The labels around the rim, dark once the needle has passed them.
        for (var n = mins; n <= maxs; n += 30)
        {
            var step = (n - mins) / 30;
            var p = Gauge(px, py, n, 55, scl);
            var passed = _needle >= (step == 0 ? n + 5 : n);
            var (r, g, b) = passed ? LabelColors[step].Passed : LabelColors[step].Ahead;
            TextCenter(Labels[8 - (n / 30 + 4)], p.X, p.Y + 5, 11, new Color(r, g, b));
        }

        // The ring: phy fills an annulus between r80 and r82 and a second between r82 and r83. Both
        // are one to two phy units thick, and a filled annulus that thin loses chunks to the fill
        // solver, so each is stroked at its mid radius and exact width instead.
        // phy's dial angles put 0° at the top; IGraphics puts 0° at 3 o'clock, so subtract 90°.
        ArcStroke(accent, 1.6f, px, py, 64.8f, 64.8f, -30, 240);
        ArcStroke(Snap(50, 204, 255, 150), 0.8f, px, py, 66f, 66f, -30, 240);

        // A two pixel strip closes the dial's open bottom.
        var left = Gauge(px, py, -120, 80, scl);
        var right = Gauge(px, py, 120, 80, scl);
        Polygon(accent, [left.X, left.X, right.X, right.X], [left.Y - 1, left.Y + 1, right.Y + 1, right.Y - 1]);

        var label = $"Speed: {MathF.Abs(Race.SpeedKmh):0.00} Km/h";
        TextCenter(label, px, left.Y + 13, 10, accent);

        var tail = Gauge(px, py, _needle + 180, 5);
        var side = Gauge(px, py, _needle + 90, 4);
        var tip = Gauge(px, py, _needle, 70, scl);
        var other = Gauge(px, py, _needle - 90, 4);
        Polygon(Snap(250, 0, 0), [tail.X, side.X, tip.X, other.X], [tail.Y, side.Y, tip.Y, other.Y]);
    }

    private float _needle = 120;

    // ── Track visor. GadgetPainter.modTrackVisor, modon 4 ────────────

    private void TrackVisor(string track)
    {
        var accent = Snap(50, 204, 255);
        var white = Snap(250, 250, 250);
        var dist = TextWidth(track, 14);
        const float y = 355;

        Rect(Snap(50, 50, 50, 220), -10, y, 35 + dist, 40);
        RectOutline(accent, 1, -10, y, 35 + dist, 40);

        // phy's double chevron tail, offset by the text width.
        for (var k = 0; k < 2; k++)
        {
            var o = 15 + dist + k * 12;
            Polygon(accent, [o, o + 10, o + 20, o + 10, o, o + 10], [y, y, y + 20, y + 41, y + 41, y + 20]);
        }

        Text(track, 11, 374, 14, white);
        Text("Now listening: ", 12, 387, 11, white);
    }
}
