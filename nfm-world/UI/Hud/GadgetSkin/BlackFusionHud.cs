using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>
/// Black Fusion, phy's second gadget skin: dark translucent panels with accent outlines, the
/// split-arc speedometer with a colour ramp, and the ring-arrow guidance.
/// </summary>
internal sealed class BlackFusionHud(IHudData race) : GadgetHud(race)
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

    // ── Position. GadgetPainter.position, poson 2 ─────────────────────

    private void Position()
    {
        var accent = Snap(50, 204, 255, 220);
        RoundedRect(Snap(50, 50, 50, 220), 13, 6, 165, 18, 5);
        RoundedRect(Snap(50, 50, 50, 220), 13, 29, 165, 26, 5);
        RoundedRectOutline(accent, 1, 13, 6, 165, 18, 5);
        RoundedRectOutline(accent, 1, 13, 29, 165, 26, 5);

        var white = Snap(250, 250, 250);
        Text("Lap:", 19, 20, 12, white);
        Text("Wasted:", 92, 20, 12, white);
        Text("Position:", 45, 45, 12, white);
        Text($"{Race.Lap} / {Race.TotalLaps}", 51, 20, 12, white);
        if (!Race.TimeTrial)
            Text($"{Race.Wasted} / {Race.Opponents}", 150, 20, 12, white);

        if (!Race.HasPosition) return;
        Text($"{Race.Position}", 98, 51, 23, accent);
        Text(OrdinalSuffix(Race.Position), 99 + TextWidth($"{Race.Position}", 23), 50, 11, white);
    }

    // ── Damage / power. GadgetPainter.status, staton 2 ───────────────

    // phy's BF_pow Area, built with the same adds and subtracts in the same order: a ring gauge
    // whose flat bottom and spokes come from the clipped rectangle and the triangle, joined to
    // the rounded panel through the half disc between them. The panel's corner radius is 7.5:
    // Java2D's RoundRectangle2D arc width is a diameter, and at 15 the cut corner drops below the
    // half disc's rim, leaving its arc sticking out above the panel.
    private static readonly ShapeRegion Panel = new ShapeRegion()
        .Add(ShapeRegion.Ellipse(632, 40, 35, 35))
        .Add(ShapeRegion.Rect(597, 40, 70, 17))
        .Subtract(ShapeRegion.Rect(597, 56, 70, 20))
        .Add(ShapeRegion.RoundRect(690, 6, 94, 50, 7.5f))
        .Add(ShapeRegion.Chord(693, 56, 50, 50, 0, 180))
        .Subtract(ShapeRegion.Ellipse(632, 40, 28, 28))
        .Add(ShapeRegion.Ellipse(632, 40, 13, 13))
        .Add(ShapeRegion.Polygon(597, 56, 631, 39, 632, 39, 666, 56))
        .Subtract(ShapeRegion.Rect(597, 56, 70, 20));

    private void Status()
    {
        var white = Snap(250, 250, 250);
        var shell = Snap(50, 50, 50, 220);
        var accent = Snap(50, 204, 255, 220);

        FillRegion(shell, Panel);
        StrokeRegion(accent, 1, Panel);

        Text("Damage", 669, 41, 11, accent);
        Text("Power", 669, 52, 11, accent);

        var damage = Math.Clamp(Race.DamageFraction, 0, 1);
        TextRight($"{(int)(damage * 100)}%", 778, 41, 11, white);
        TextRight($"{(int)(Math.Clamp(Race.PowerFraction, 0, 1) * 100)}%", 778, 52, 11, white);

        // Damage: fourteen bars, the first two shorter and stepped, empty ones in the panel colour.
        var lit = DamageColor(damage, alpha: 200);
        var filled = (int)(damage * 110);
        for (var i = 0; i < 110; i += 8)
            Rect(i < filled ? lit : shell, 669 + i, i == 0 ? 18 : i == 8 ? 15 : 12, 6, i == 0 ? 11 : i == 8 ? 14 : 17);

        // The power needle in the ring, phy's 230° sweep from 115°.
        var degrees = 115 - (int)(Math.Clamp(Race.PowerFraction, 0, 1) * 230f);
        var tail = Gauge(632, 40, degrees + 180, 5);
        var left = Gauge(632, 40, degrees + 90, 3.5f);
        var tip = Gauge(632, 40, degrees, 32);
        var right = Gauge(632, 40, degrees - 90, 3.5f);
        Polygon(Snap(250, 0, 0), [tail.X, left.X, tip.X, right.X], [tail.Y, left.Y, tip.Y, right.Y]);
    }

    // ── Opponents. GadgetPainter.opponentStatus, opstaton 2 ──────────

    private void Opponents()
    {
        const int maxRows = 11;
        var shell = Snap(50, 50, 50, 220);
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250);
        var row = 0;
        foreach (var racer in Race.Racers)
        {
            if (racer.Wasted) continue;
            if (row >= maxRows) break;
            var y = 60 + 30 * row;
            // phy's tapered row: a trapezoid narrowing on the right.
            Span<float> xs = [682, 793, 787, 676];
            Span<float> ys = [y, y, y + 25, y + 25];
            Polygon(shell, xs, ys);
            TextCenter(racer.Name, 735, y + 12, 11, white);
            Text($"{row + 1}", 769, y + 22, 23, white);
            var barWidth = Math.Clamp(racer.Damage, 0, 1) * 60;
            var color = racer.Damage < 0.25f ? Snap(50, 250, 0, 220)
                : racer.Damage < 0.6f ? Snap(250, 250, 0, 220)
                : racer.Damage < 0.8f ? Snap(250, 140, 0, 220)
                : Snap(250, 30, 0, 220);
            for (var j = 0; j < barWidth; j += 3)
                Rect(color, 694 + j, y + 16, 2, 6);
            row++;
        }
    }

    // ── Guidance arrow. GadgetPainter.arrow, arrowon 2: the ring with a wedge ──

    private void Arrow()
    {
        if (Race.ArrowAngle is not { } angle) return;
        var shell = Snap(50, 50, 50, 220);
        RoundedRect(shell, 328, 13, 149, 49, 24);
        var accent = Race.ArrowAtCars ? Snap(50, 204, 255, 220) : Snap(250, 250, 20);
        var white = Snap(250, 250, 250);

        var name = Race.ArrowTarget;
        Text(name, 384, 37, MathF.Min(15, 83f / MathF.Max(1, name.Length) * 1.6f), white);
        if (Race.ArrowDistanceMeters is { } meters)
        {
            TextCenter($"{(int)(meters / 10) * 10}", 349, 38, 13, white);
            Text("mts", 341, 48, 11, Snap(220, 220, 220));
        }

        // The ring: an annulus with a triangular wedge pointing at the target. Both go in one
        // path so the wedge joins the ring rather than sitting beside it.
        var px = X(349);
        var py = Y(37);
        G.SetColor(accent);
        G.BeginPath();
        G.Ellipse(px, py, S(24), S(24));
        G.MarkHole();
        G.Ellipse(px, py, S(18), S(18));
        var nose = Polar(px, py, angle - 180, S(36));
        var left = Polar(px, py, angle + 30 - 180, S(24));
        var right = Polar(px, py, angle - 30 - 180, S(24));
        G.MoveTo(nose.X, nose.Y);
        G.LineTo(left.X, left.Y);
        G.LineTo(right.X, right.Y);
        G.ClosePath();
        G.Fill();
    }

    // ── Radar. GadgetPainter.radarstat, radaron 2 ─────────────────────

    private void Radar()
    {
        if (Race.ClientPosition is null) return;
        var side = S(172);
        var x = X(6);
        var y = Y(290);
        RoundedRect(Snap(50, 50, 50, 220), 6, 290, 172, 120, 10);
        ClipRect(6, 290, 172, 120, 10);
        GadgetRadar.Draw(this, Race, new Vector2(x + side / 2, y + S(120) / 2), side);
        G.ResetScissor();
        RoundedRectOutline(Snap(50, 204, 255, 220), 1, 6, 290, 172, 120, 10);
    }

    // ── Chronometer. Chronometer.paint, style 2 ──────────────────────

    private void Chronometer()
    {
        var splits = Race.LapSplits;
        var rows = Math.Clamp(splits.Count, 1, 10);
        var h = 20 * (rows + 1);
        RoundedRect(Snap(50, 50, 50, 220), 602, 65, 177, h, 7);
        RoundedRectOutline(Snap(50, 204, 255, 220), 1, 602, 65, 177, h, 7);

        var white = Snap(250, 250, 250);
        TextCenter("# Lap", 624, 80, 11, white);
        TextCenter("Lap Time", 679, 80, 11, white);
        TextCenter("Split Time", 746, 80, 11, white);
        Line(Snap(50, 50, 50), 645, 65, 645, 65 + h - 1);
        Line(Snap(50, 50, 50), 713, 65, 713, 65 + h - 1);

        var cumulative = 0;
        for (var i = 0; i < rows; i++)
        {
            cumulative += splits[i];
            var y = 65 + 20 * (i + 1);
            Line(Snap(50, 204, 255, 220), 602, y, 779, y);
            TextCenter($"{i + 1}", 624, y + 15, 11, white);
            TextCenter(Time(splits[i]), 679, y + 15, 11, white);
            TextCenter(Time(cumulative), 746, y + 15, 11, white);
        }
    }

    // ── Speedometer. GadgetPainter.speedometer, speedon 2, ported line for line ──

    private const int SpeedoX = 730;
    private const int SpeedoY = 379;

    // The dial's pie and the bezel polygon that closes its open bottom, filled as one shape.
    private static readonly ShapeRegion Dial = BuildDial();

    private static ShapeRegion BuildDial()
    {
        var p1 = Gauge(SpeedoX + 1, SpeedoY + 2, -120, 62);
        var p2 = Gauge(SpeedoX, SpeedoY - 1, -120, 62);
        var p3 = Gauge(SpeedoX - 1, SpeedoY - 1, 120, 62);
        return new ShapeRegion()
            .Add(ShapeRegion.Pie(729.5f, 378.5f, 62.5f, 62.5f, -30, 240))
            .Add(ShapeRegion.Polygon(
                p1.X,
                p1.Y,
                p2.X,
                p2.Y,
                SpeedoX + 1,
                SpeedoY,
                SpeedoX - 2,
                SpeedoY,
                p3.X,
                p3.Y,
                p3.X - 1,
                p3.Y + 3
            )
            );
    }

    private void Speedometer()
    {
        const int px = SpeedoX;
        const int py = SpeedoY;
        var fraction = Math.Clamp(MathF.Abs(Race.SpeedFraction ?? 0), 0, 1.1f);
        var goingFast = fraction > 1.01f;

        FillRegion(Snap(50, 50, 50, 220), Dial);
        ArcStroke(Snap(40, 40, 40), 6, 729.5f, 378.5f, 62.5f, 62.5f, -30, 240);
        var rim = goingFast ? Snap(250, 20, 20, 220) : Snap(50, 204, 255, 220);
        ArcStroke(rim, 2, 729.5f, 378.5f, 62.5f, 62.5f, -30, 240);

        // The ramp's twelve segments. phy sets the colour at i = 3..0 only when not speeding and
        // lets it carry, so a speeding car keeps the rim's red on every one.
        var color = rim;
        for (var i = 11; i >= 0; --i)
        {
            if (!goingFast)
            {
                if (i == 3) color = Snap(50, 204, 120, 220);
                if (i == 2) color = Snap(250, 250, 20, 220);
                if (i == 1) color = Snap(250, 150, 20, 220);
                if (i == 0) color = Snap(250, 20, 20, 220);
            }

            ArcStroke(color, 5, 729.5f, 378.5f, 54.5f, 54.5f, -30 + (int)(20.8 * i), 10);
        }

        Oval(Snap(40, 40, 40), px, py, 8, 8);

        var degrees = Math.Max(-115, 110 - (int)(fraction * 220.0));
        var hubTail = Gauge(px, py, degrees + 68, 8);
        var hubTip = Gauge(px, py, degrees, 25);
        var hubHead = Gauge(px, py, degrees - 68, 8);
        Polygon(Snap(40, 40, 40), [hubTail.X, hubTip.X, hubHead.X], [hubTail.Y, hubTip.Y, hubHead.Y]);

        if (goingFast)
            Text("[Boost]", 711, 348, 11, Snap(255, 140, 10));
        else if (Race.SpeedFraction is < 0)
            Text("[Rev]", 717, 348, 11, Snap(100, 140, 10));

        var tail = Gauge(px, py, degrees + 180, 5);
        var left = Gauge(px, py, degrees + 90, 3.2f);
        var tip = Gauge(px, py, degrees, 60);
        var right = Gauge(px, py, degrees - 90, 3.2f);
        Polygon(Snap(250, 0, 0), [tail.X, left.X, tip.X, right.X], [tail.Y, left.Y, tip.Y, right.Y]);

        TextCenter(
            $"{MathF.Abs(Race.SpeedKmh):0.00} Km/h",
            px,
            406,
            14,
            goingFast ? Snap(250, 0, 0) : Snap(250, 250, 250)
        );
    }

    // ── Track visor. GadgetPainter.modTrackVisor, modon 2 ────────────

    private void TrackVisor(string track)
    {
        var shell = Snap(50, 50, 50, 220);
        var accent = Snap(50, 204, 255, 220);
        var boxWidth = 35 + TextWidth(track, 14);
        RoundedRect(shell, -10, 355, boxWidth, 40, 12);

        // phy's pointed banner tab with the double chevron.
        Span<float> tabXs = [0, 85, 95, 85, 0];
        Span<float> tabYs = [340, 340, 349, 358, 358];
        Polygon(accent, tabXs, tabYs);
        Span<float> chevronXs = [5, 15, 25, 15, 5, 15];
        Span<float> chevronYs = [340, 340, 349, 358, 358, 349];
        Polygon(Snap(50, 50, 50), chevronXs, chevronYs);

        RoundedRectOutline(accent, 1, -10, 355, boxWidth, 40, 12);
        Text(track, 11, 374, 14, Snap(250, 250, 250));
        Text("Now listening: ", 5, 353, 11, Snap(250, 250, 250));
    }
}
