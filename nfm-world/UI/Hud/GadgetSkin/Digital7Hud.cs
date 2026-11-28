using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>
/// Digital 7, phy's third gadget skin: the Digital-7 seven-segment font for every number, dark
/// rounded panels, the LED bar-graph speedometer and the swept-arc guidance.
/// </summary>
internal sealed class Digital7Hud(IHudData race) : GadgetHud(race)
{
    protected override void DrawGadgets()
    {
        Anchor(HudAnchor.TopLeft);
        Position();
        Anchor(HudAnchor.TopRight);
        Status();
        if (!Race.TimeTrial && Race.Racers.Count > 1)
            Opponents();
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

    // ── Position. GadgetPainter.position, poson 3 ─────────────────────

    private void Position()
    {
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250, 220);
        RoundedRect(Snap(0, 0, 0, 100), 6, 6, 120, 80, 10);

        if (Race.TotalLaps > 0)
        {
            Text("Lap:", 12, 20, 11, accent);
            TextRight($"{Race.Lap} / {Race.TotalLaps}", 120, 21, 13, white, FontFamily.Digital7, false);
        }

        Text("Wasted:", 12, 35, 11, accent);
        Text("Position:", 12, 50, 11, accent);

        var pos = Race.HasPosition ? Race.Position : 0;
        if (!Race.TimeTrial)
            TextRight($"{Race.Wasted} / {Race.Opponents}", 120, 36, 13, white, FontFamily.Digital7, false);

        if (pos > 0)
        {
            TextRight($"{pos}", 104, 80, 52, accent, FontFamily.Digital7);
            Text(OrdinalSuffix(pos), 108, 79, 13, white, FontFamily.Digital7, false);
        }
    }

    // ── Damage / power. GadgetPainter.status, staton 3 ───────────────

    private static readonly ShapeRegion[] PowerSegments = Enumerable.Range(0, 12)
        .Select(i => new ShapeRegion()
            .Add(ShapeRegion.Pie(634, 27, 15, 15, -185 + i * 21, 14))
            .Subtract(ShapeRegion.Ellipse(634, 27, 8, 8))
        )
        .ToArray();

    private void Status()
    {
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250, 220);
        RoundedRect(Snap(0, 0, 0, 100), 604, 6, 191, 42, 10);

        Text("DAMAGE", 661, 43, 11, accent);
        Text("POW", 610, 21, 11, accent);

        // Power: phy's 12-arc dial, its 16px hub clipped out of every segment.
        var power = Math.Clamp(Race.PowerFraction, 0, 1);

        for (var i = 0; i < PowerSegments.Length; i++)
        {
            if (i * 20 < power * 240)
                FillRegion(accent, PowerSegments[i]);
        }

        var damage = Math.Clamp(Race.DamageFraction, 0, 1);
        var damagePercent = (int)(damage * 100);
        var damageColor = damagePercent >= 80 ? Snap(250, 30, 0, 220)
            : damagePercent >= 45 ? Snap(250, 50, 0, 220)
            : accent;
        TextRight($"{damagePercent}%", 790, 43, 11, white);

        // Damage: the segmented bar, with the sweep groove phy animates across it.
        const int grooveWidth = 4;
        var barWidth = damage * 125;
        for (var i = 0; i + grooveWidth <= barWidth; i += grooveWidth)
            Rect(damageColor, 661 + i, 13, 2, 17);

        // The groove highlight at the leading lit segment.
        var groove = (int)(Clock * 30) % 125;
        if (damagePercent >= 50 && groove < barWidth)
            Rect(Snap(250, 250, 250, 220), 661 + groove, 11, 2, 21);
    }

    // ── Opponents. GadgetPainter.opponentStatus, opstaton 3 ──────────

    private void Opponents()
    {
        const int maxRows = 11;
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250, 220);
        var row = 0;

        foreach (var racer in Race.Racers)
        {
            if (racer.Wasted)

                continue;

            if (row >= maxRows)

                break;
            var y = 58 + 30 * row;
            RoundedRect(Snap(0, 0, 0, 100), 684, y, 111, 25, 8);
            if (racer.LockSlot is > 0 and <= 10)
                Rect(Snap(0, 0, 0, 150), 684, y, 36, 25);
            Text($"{row + 1}", 690, y + 22, 28, accent, FontFamily.Digital7, false);

            var damagePercent = (int)(Math.Clamp(racer.Damage, 0, 1) * 100);
            var color = damagePercent >= 80 ? Snap(250, 30, 0, 220)
                : damagePercent >= 45 ? Snap(250, 250, 0, 220)
                : Snap(50, 204, 255, 220);
            var lit = Math.Clamp(racer.Damage, 0, 1) * 60;
            for (var j = 0; j < lit; j += 3)
                Rect(color, 720 + j, y + 16, 2, 6);

            TextCenter(racer.Name, 751, y + 12, 11, white);
            row++;
        }
    }

    // ── Guidance arrow. GadgetPainter.arrow, arrowon 3 ───────────────

    private void Arrow()
    {
        if (Race.ArrowAngle is not { } angle)

            return;
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250, 220);
        RoundedRect(Snap(0, 0, 0, 100), 320, 6, 160, 42, 10);

        var name = Race.ArrowTarget;
        Text(name, 326, 24, MathF.Min(14, 72f / MathF.Max(1, name.Length) * 1.6f), accent);
        Text(Race.ArrowTarget, 326, 40, 11, white, FontFamily.DroidSans, false);

        // phy's swept arc: 17 segments around the pill, the three nearest the heading lit.
        ClipRect(320, 6, 160, 42, 10);
        for (var i = 0; i < 17; i++)
            ArcFill(Snap(50, 204, 255, 150), 439, 24, 35, 12, -185 + i * 21, 14);

        var lit = (int)(-angle / 360f * 16f);

        for (var i = -1; i <= 1; i++)
        {
            var seg = ((lit + i) % 17 + 17) % 17;
            ArcFill(accent, 439, 24, 35, 12, -100 + seg * 21, 14);
        }

        G.ResetScissor();
    }

    // ── Radar. GadgetPainter.radarstat, radaron 3 ─────────────────────

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
        RoundedRectOutline(Snap(50, 204, 255, 220), 1, 6, 290, 172, 120, 10);
        Text(Race.ArrowAtCars ? "RADAR" : "NAV", 12, 304, 11, Snap(50, 204, 255, 220));
    }

    // ── Chronometer. Chronometer.paint, style 3 ──────────────────────

    private void Chronometer()
    {
        var splits = Race.LapSplits;
        var rows = Math.Clamp(splits.Count, 1, 10);
        var h = 20 * (rows + 1);
        RoundedRect(Snap(0, 0, 0, 100), 602, 65, 177, h, 7);

        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250, 220);
        TextCenter("# Lap", 624, 80, 11, accent);
        TextCenter("Lap Time", 679, 80, 11, accent);
        TextCenter("Split Time", 746, 80, 11, accent);
        Line(Snap(50, 50, 50), 645, 65, 645, 65 + h - 1);
        Line(Snap(50, 50, 50), 713, 65, 713, 65 + h - 1);

        var cumulative = 0;

        for (var i = 0; i < rows; i++)
        {
            cumulative += splits[i];
            var y = 65 + 20 * (i + 1);
            Line(Snap(0, 0, 0, 110), 602, y, 779, y);
            TextCenter($"{i + 1}", 624, y + 15, 12, white, FontFamily.Digital7, false);
            TextCenter(Time(splits[i]), 679, y + 15, 12, white, FontFamily.Digital7, false);
            TextCenter(Time(cumulative), 746, y + 15, 12, white, FontFamily.Digital7, false);
        }
    }

    // ── Speedometer. GadgetPainter.speedometer, speedon 3 ────────────

    private void Speedometer()
    {
        var accent = Snap(50, 204, 255, 220);
        RoundedRect(Snap(0, 0, 0, 100), 677, 330, 121, 80, 10);

        Text("SPEED", 685, 382, 11, accent);

        // The LED bar graph: 24 columns, 2px wide, taller at the top of the range.
        var speed = Math.Clamp(MathF.Abs(Race.SpeedFraction ?? 0), 0, 1);
        var lit = speed * 94;

        for (var i = 0; i + 4 <= lit; i += 4)
        {
            float h;
            if (i > 38 && i <= 72)
                h = 17 + (i - 36) / 32f * 12f;
            else
                h = i > 70 ? 30 : 17;
            Rect(accent, 685 + i, 402 - h, 2, h);
        }

        Rect(accent, 685, 403, 94, 2);

        // Boost arrows over the graph when past the top.

        if (Race.SpeedFraction > 1f)
        {
            var orange = Snap(250, 20, 20, 220);

            for (var i = 0; i < 2; i++)

            for (var j = 0; j < 5; j++)
            {
                Line(orange, 784 + j, 376 + i * 6 - j, 784 + j, 380 + i * 6 - j);
                Line(orange, 793 - j, 377 + i * 6 - j, 793 - j, 381 + i * 6 - j);
            }
        }

        // The digital readout: integer and centesimal digits, in Digital-7.
        var white = Snap(250, 250, 250);
        var kph = MathF.Abs(Race.SpeedKmh);
        Text("KPH", 771, 344, 11, accent);
        TextRight($"{(int)kph:000}", 762, 368, 47, white, FontFamily.Digital7);
        Rect(white, 763, 365, 3, 3);
        Text($"{(int)(kph * 100) % 100:00}", 767, 368, 25, white, FontFamily.Digital7);
    }

    // ── Track visor. GadgetPainter.modTrackVisor, modon 3 ────────────

    private void TrackVisor(string track)
    {
        var accent = Snap(50, 204, 255, 220);
        var white = Snap(250, 250, 250, 220);
        var boxWidth = 35 + TextWidth(track, 15, FontFamily.Digital7, false);
        Rect(Snap(0, 0, 0, 100), -10, 355, boxWidth, 40);
        Rect(accent, 25 + TextWidth(track, 15, FontFamily.Digital7, false), 355, 10, 41);
        RectOutline(accent, 1, -10, 355, boxWidth, 40);
        Text(track, 11, 374, 15, white, FontFamily.Digital7, false);
        Text("Now listening: ", 12, 353, 11, white);
    }
}
