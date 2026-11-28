using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>DSmod2, phy's first gadget skin: outlined boxes, Yukari labels, blue accents.</summary>
internal sealed class Dsmod2Hud(IHudData race) : GadgetHud(race)
{
    protected override void DrawGadgets()
    {
        Anchor(HudAnchor.TopLeft);
        Position();
        Anchor(HudAnchor.TopRight);
        Status();
        if (!Race.TimeTrial && Race.Racers.Count > 1)
            Opponents();
        // phy only paints the chronometer in singleplayer, where it fills the space the
        // opponent rows use in a race.
        if (Race.Opponents == 0 && Race.LapSplits.Count > 1)
            Chronometer();
        Anchor(HudAnchor.Top);
        Arrow();
        Anchor(HudAnchor.BottomLeft);
        Radar();
        if (Race.MusicTrack is { } track)
            TrackVisor(track);
        Anchor(HudAnchor.BottomRight);
        Speedometer();
    }

    // ── Position: lap / wasted / big ordinal. GadgetPainter.position, poson 1 ──

    private void Position()
    {
        RoundedRect(Snap(0, 0, 0, 100), 13, 6, 173, 18, 5);
        RoundedRect(Snap(0, 0, 0, 100), 35, 29, 73, 18, 5);

        var white = Snap(250, 250, 250);
        Text("wasted", 90, 20, 15, white, FontFamily.Yukari);
        Text("position", 41, 42, 15, white, FontFamily.Yukari);

        var blue = Snap(30, 70, 150);

        if (Race.TotalLaps > 0)
        {
            Text("lap", 19, 20, 15, white, FontFamily.Yukari);
            Text($"{Race.Lap} / {Race.TotalLaps}", 51, 20, 13, blue, FontFamily.Yukari, false);
        }

        if (!Race.TimeTrial)
            Text($"{Race.Wasted} / {Race.Opponents}", 154, 20, 13, blue, FontFamily.Yukari, false);

        if (!Race.HasPosition)

            return;
        var pos = Race.Position;
        var width = TextWidth($"{pos}", 72, FontFamily.Yukari);
        Text($"{pos}", 112, 82, 72, Snap(0, 0, 0, 110), FontFamily.Yukari);
        Text($"{pos}", 110, 80, 72, Snap(50, 204, 255), FontFamily.Yukari);
        Text(OrdinalSuffix(pos), 113 + width, 77, 32, Snap(0, 0, 0, 110), FontFamily.Yukari);
        Text(OrdinalSuffix(pos), 111 + width, 75, 32, white, FontFamily.Yukari);
    }

    // ── Damage / power bars. GadgetPainter.status, staton 1 ──────────

    private void Status()
    {
        RoundedRect(Snap(0, 0, 0, 100), 618, 6, 174, 18, 5);
        RoundedRect(Snap(0, 0, 0, 100), 625, 26, 167, 18, 5);

        var white = Snap(250, 250, 250);
        Text("damage", 624, 19, 15, white, FontFamily.Yukari);
        Text("power", 633, 39, 15, white, FontFamily.Yukari);

        var frame = Snap(0, 40, 120);
        Bar(689, 10, Race.DamageFraction, DamageColor(Race.DamageFraction), frame);
        Bar(689, 30, Race.PowerFraction, Snap(50, 204, 255), frame);
    }

    private void Bar(float x, float y, float value, Color fill, Color frame)
    {
        RectOutline(frame, 2, x, y, 100, 10);
        var w = 100 * Math.Clamp(value, 0, 1);
        if (w > 0)
            Rect(fill, x, y, w, 10);
    }

    // ── Opponent rows. GadgetPainter.opponentStatus, opstaton 1 ──────

    private void Opponents()
    {
        const int maxRows = 11;
        var row = 0;

        foreach (var racer in Race.Racers)
        {
            if (racer.Wasted)

                continue;

            if (row >= maxRows)

                break;
            var y = 58 + 30 * row;

            if (racer.LockSlot is > 0 and <= 10)
            {
                Rect(Snap(0, 40, 120), 671, y, 5, 25);
                Text($"{racer.LockSlot % 10}", 679, y + 17, 11, Snap(0, 0, 0));
            }

            Text(Ordinal(racer.Position), 685, y + 17, 11, Snap(30, 70, 150));
            TextCenter(racer.Name, 747, y + 12, 11, Snap(0, 0, 0));
            var barWidth = 60 * Math.Clamp(racer.Damage, 0, 1);
            if (barWidth > 0)
                Rect(DamageColor(racer.Damage, blink: false), 717, y + 17, barWidth, 4);
            RectOutline(Snap(0, 0, 0), 1, 716, y + 16, 62, 6);
            row++;
        }
    }

    // ── Guidance arrow. GadgetPainter.arrow, arrowon 1: the classic arrow in skin colours ──

    private void Arrow()
    {
        if (Race.Spectating)

            return;
        if (Race.ArrowAngle is not { } angle)

            return;
        if (!Race.ArrowAtCars && MathF.Abs(angle) <= 7 && !Race.IsPreview)

            return;

        // xtGraphics.arrow: rotate the seven X/Z vertices, then project at y=-90. The same shape
        // as the classic skin's, so switching skins keeps the arrow and changes its palette.
        ReadOnlySpan<float> xs = [0, -35, -15, -15, 15, 15, 35];
        ReadOnlySpan<float> zs = [110, 50, 50, -50, -50, 50, 50];
        Span<int> px = stackalloc int[7];
        Span<int> py = stackalloc int[7];
        var radians = angle * (MathF.PI / 180);
        var sin = MathF.Sin(radians);
        var cos = MathF.Cos(radians);

        // xt.rot about (400, 700), then xt.xs / xt.ys: a perspective projection about
        // (focus_point 400, cx 400, cy 225). The vertices sit at y = -90 before it.

        for (var i = 0; i < xs.Length; i++)
        {
            var dx = xs[i];
            var dz = zs[i]; // already z − 700, phy's rotation centre's z
            var vx = 400f + dx * cos - dz * sin;
            var vz = 700f + dx * sin + dz * cos;
            const float vy = -90f;
            var z = MathF.Max(50f, vz);
            px[i] = (int)X((z - 400) * (400 - vx) / z + vx);
            py[i] = (int)Y((z - 400) * (225 - vy) / z + vy);
        }

        // arrowon 1 is only blue while aiming at cars; in track mode phy draws it green.
        var turn = MathF.Abs(angle);

        if (Race.ArrowAtCars)
        {
            G.SetColor(Snap(130, 170, 250, 180));
            G.FillPolygon(px, py, 7);
            G.SetColor(Snap(30, 70, 150));
        }
        else
        {
            G.SetColor(turn <= 45 ? Snap(190, 255, 0, (int)(255 * (45 - turn) / 45)) : Snap(190, 255, 0, 200));
            G.FillPolygon(px, py, 7);
            G.SetColor(Snap(115, 170, 0));
        }

        G.SetStrokeWidth(MathF.Max(1, _scale));
        G.DrawPolygon(px, py, 7);
    }

    // ── Radar. GadgetPainter.radarstat, radaron 1 ─────────────────────

    private void Radar()
    {
        if (Race.ClientPosition is null)

            return;
        var side = S(172);
        var x = X(6);
        var y = Y(290);
        RoundedRect(Snap(0, 0, 0, 100), 6, 290, 172, 120, 10);
        ClipRect(6, 290, 172, 120, 10);
        GadgetRadar.Draw(this, Race, new Vector2(x + side / 2, y + S(120) / 2), side);
        G.ResetScissor();
        RoundedRectOutline(Snap(50, 204, 255), 1, 6, 290, 172, 120, 10);
    }

    // ── Chronometer: the lap/split table. Chronometer.paint, style 1 ──

    private void Chronometer()
    {
        var splits = Race.LapSplits;
        var rows = Math.Clamp(splits.Count, 1, 10);
        var h = 20 * (rows + 1);
        RoundedRect(Snap(50, 204, 255, 100), 602, 65, 177, h, 7);

        var black = Snap(0, 0, 0);
        TextCenter("# Lap", 624, 80, 11, black);
        TextCenter("Lap Time", 679, 80, 11, black);
        TextCenter("Split Time", 746, 80, 11, black);
        Line(black, 645, 65, 645, 65 + h - 1);
        Line(black, 713, 65, 713, 65 + h - 1);

        var cumulative = 0;
        var white = Snap(250, 250, 250);

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

    // ── Speedometer. GadgetPainter.speedometer, speedon 1, ported line for line ──

    // The dial's pie plus the panel polygon that closes it, filled as one translucent shape. The
    // pie's arc ends at (671.2, 389.8) while the polygon starts at (670, 390), so the triangle
    // between them closes a wedge that would otherwise show as a transparent hairline.
    private static readonly ShapeRegion Backdrop = new ShapeRegion()
        .Add(ShapeRegion.Pie(746, 395, 75, 75, 61, 115))
        .Add(ShapeRegion.Polygon(782, 330, 782, 410, 618, 410, 623, 390, 670, 390, 746, 395))
        .Add(ShapeRegion.Polygon(746, 395, 670, 390, 671.2f, 389.76f));

    private void Speedometer()
    {
        var accent = Snap(50, 204, 255);
        var fraction = Math.Clamp(MathF.Abs(Race.SpeedFraction ?? 0), 0, 1.2f);
        var goingFast = fraction > 1f;

        FillRegion(goingFast ? Snap(180, 0, 0, 100) : Snap(0, 0, 0, 100), Backdrop);

        ArcStroke(accent, 1, 746, 395, 75, 75, 61, 115);
        Line(accent, 782, 330, 782, 410);
        Line(accent, 782, 410, 618, 410);
        Line(accent, 618, 410, 623, 390);
        Line(accent, 623, 390, 670, 390);

        // phy put the ticks and needle at (750, 400) but drew the outlining arc about (746, 395)
        // at radius 75, so ticks crossed the rim on one side and stopped short of it on the other.
        // Both share the arc's centre here and every tick ends on its radius.
        const int px = 746;
        const int py = 395;
        const float rim = 75;
        var degrees = MathF.Max(-20, 90 - (int)(fraction * 110f));

        for (var i = 0; i <= 90; i += 30)
        {
            var a = Gauge(px, py, i, rim - 20);
            var b = Gauge(px, py, i, rim);
            Line(accent, a.X, a.Y, b.X, b.Y);
        }

        for (var i = -15; i <= 90; i += 30)
        {
            var a = Gauge(px, py, i, rim);
            var b = Gauge(px, py, i, rim - 10);
            Line(accent, a.X, a.Y, b.X, b.Y);
        }

        // phy draws this in the colour already set: the dial's accent.
        Text("Speed:", 628, 405, 11, accent);

        var needle = Snap(250, 0, 0);
        var tail = Gauge(px, py, degrees + 180, 5);
        var left = Gauge(px, py, degrees + 90, 4);
        var tip = Gauge(px, py, degrees, rim - 10);
        var right = Gauge(px, py, degrees - 90, 4);
        Polygon(needle, [tail.X, left.X, tip.X, right.X], [tail.Y, left.Y, tip.Y, right.Y]);

        var speedText = $"{MathF.Abs(Race.SpeedKmh):0.00} Km/h";
        TextRight(speedText, 778, 406, 14, goingFast ? Snap(250, 0, 0) : Snap(250, 250, 250));
        if (goingFast)
            Text("[Boost]", 625, 383, 11, Snap(255, 140, 10));
        else if (Race.SpeedFraction is < 0)
            Text("[Rev]", 632, 383, 11, Snap(100, 140, 10));
    }

    // ── Track visor. GadgetPainter.modTrackVisor, modon 1 ────────────

    private void TrackVisor(string track)
    {
        var boxWidth = 35 + TextWidth(track, 14);
        RoundedRect(Snap(30, 70, 150), -10, 340, 100, 18, 12);
        RoundedRectOutline(Snap(0, 0, 0, 100), 1, -10, 355, boxWidth, 40, 12);
        Text(track, 11, 374, 14, Snap(20, 20, 20));
        Text("Now listening: ", 5, 353, 11, Snap(220, 220, 220));
    }
}
