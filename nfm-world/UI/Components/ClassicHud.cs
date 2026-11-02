using System.Diagnostics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary;
using NFMWorldLibrary.Hud;
using NFMWorld.UI.Hud;

namespace NFMWorld.UI.Components;

public sealed class ClassicHud(IHudData race) : Component
{
    private IImage? _damage = HudImages.TryLoad("data/images/damage.hud.svg", SnapMultiplier(), 225);
    private IImage? _power = HudImages.TryLoad("data/images/power.hud.svg", SnapMultiplier(), 225);
    private Color3 _svgSnap = World.Snap;
    private long _lastFrame = Stopwatch.GetTimestamp();
    private float _clock;
    private float _arrowAngle;
    private bool _hadArrow;
    private bool? _atCars;
    private float _modeMessageUntil;

    private IHudData Race => race;

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var now = Stopwatch.GetTimestamp();
        var dt = Math.Clamp((float)Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds, 0, 0.1f);
        _lastFrame = now;
        if (!Race.Paused) _clock += dt;
        var scale = MathF.Min(size.X / 960, size.Y / 540) * HudScaleSettings.ClassicDefault * SettingsMenu.HudScale;
        if (scale <= 0) return;
        var bounds = HudSafeZone.Bounds(position, size);
        position = new Vector2(bounds.X, bounds.Y);
        size = new Vector2(bounds.Width, bounds.Height);

        if (_svgSnap != World.Snap)
        {
            if (_damage is not null) _damage = HudImages.TryLoad("data/images/damage.hud.svg", SnapMultiplier(), 225);
            if (_power is not null) _power = HudImages.TryLoad("data/images/power.hud.svg", SnapMultiplier(), 225);
            _svgSnap = World.Snap;
        }

        G.SaveState();
        try
        {
            var left = position.X;
            var top = position.Y;
            var right = position.X + size.X;
            Text($"LAP: {Race.Lap} / {Race.TotalLaps}", left + 14 * scale, top + 5 * scale, scale, adventure: true);
            if (!Race.TimeTrial)
            {
                Text($"WASTED: {Race.Wasted} / {Race.Opponents}", left + 128 * scale, top + 5 * scale, scale, adventure: true);
            }
            if (Race.HasPosition) PositionReadout(left + 30 * scale, top + 24 * scale, scale);
            var timerY = top + (Race.HasPosition ? 49 : 30) * scale;
            Text(Race.TimeTrial ? "TIME TRIAL" : "TIME", left + 14 * scale, timerY, scale, adventure: true);
            Digits(HudText.FormatTime(Race.LapTimeMs), left + 14 * scale, timerY + 16 * scale, 22 * scale);

            var speedX = left + size.X / 2;
            var speedY = top + size.Y - 55 * scale;
            var speed = Race.SpeedKmh;
            Digits($"{speed}", speedX, speedY, 30 * scale, center: true);
            Text("KM/H", speedX, speedY + 34 * scale, scale, adventure: true, center: true);

            Meter(_damage, right - 166 * scale, top + 5 * scale, scale, Race.DamageFraction, true);
            Meter(_power, right - 166 * scale, top + 24 * scale, scale, Race.PowerFraction, false);
            Arrow(position, size, scale, Race.Paused ? 0 : dt);

            if (!Race.TimeTrial && Race.Racers.Count > 1)
            {
                var y = top + 80 * scale;
                foreach (var racer in Race.Racers)
                {
                    if (y + 22 * scale > top + size.Y - 30 * scale) break;
                    if (racer.IsClient)
                    {
                        G.SetColor(Snap(new Color(90, 195, 255, 60)));
                        G.FillRect((int)(right - 144 * scale), (int)y, (int)(136 * scale), (int)(24 * scale));
                    }
                    Text(Ordinal(racer.Position), right - 138 * scale, y + 5 * scale, scale, color: new Color(20, 20, 120));
                    Text(racer.Name, right - 110 * scale, y, scale);
                    var barX = right - 110 * scale;
                    var barY = y + 15 * scale;
                    G.SetColor(new Color(0, 0, 0));
                    G.FillRect((int)barX, (int)barY, (int)(82 * scale), Math.Max(1, (int)(5 * scale)));
                    G.SetColor(Snap(190, 240, 245));
                    G.FillRect((int)(barX + scale), (int)(barY + scale), (int)(80 * scale), Math.Max(1, (int)(3 * scale)));
                    G.SetColor(Snap(244, 244, 11));
                    G.FillRect((int)(barX + scale), (int)(barY + scale), (int)(80 * scale * Clamp(racer.Damage)), Math.Max(1, (int)(3 * scale)));
                    if (racer.Wasted) Text("WASTED", right - 70 * scale, y, scale, color: new Color(200, 20, 0));
                    y += 29 * scale;
                }
            }
        }
        finally
        {
            G.RestoreState();
        }
    }

    private void Meter(IImage? image, float x, float y, float scale, float value, bool damage)
    {
        const float width = 156;
        const float artWidth = 1403.6f;
        const float height = width * 134.36f / artWidth;
        // Shared transparent opening in the current meter artwork.
        var fillX = x + width * 491.88f / artWidth * scale;
        var fillY = y + width * 28.08f / artWidth * scale;
        var fillWidth = width * (1313.48f - 491.88f) / artWidth * scale;
        var fillHeight = width * (103.51f - 28.08f) / artWidth * scale;
        G.SetColor(Snap(new Color(190, 225, 230, 90)));
        G.FillRect((int)fillX, (int)fillY, (int)MathF.Ceiling(fillWidth), (int)MathF.Ceiling(fillHeight));
        G.SetColor(damage ? DamageColor(value) : Snap(value >= 1 ? 64 : 128, (int)(190 + value * 98 * 0.37f), 244));
        if (value > 0)
            G.FillRect((int)fillX, (int)fillY, (int)MathF.Ceiling(fillWidth * value), (int)MathF.Ceiling(fillHeight));
        if (image is not null)
            G.DrawImage(image, (int)x, (int)y, (int)(width * scale), (int)(height * scale));
    }

    private Color DamageColor(float damage)
    {
        var amount = (int)(98 * damage);
        var green = amount > 33 ? (int)(244 - 233f * (amount - 33) / 65) : 244;
        if (amount > 70)
        {
            var period = (168 - amount * 1.5f) / 30;
            var phase = _clock % period;
            if (phase < 10f / 30 && (int)(phase * 30) % 2 == 0) green = 170;
        }
        return Snap(244, green, 11);
    }

    private void Arrow(Vector2 position, Vector2 size, float scale, float dt)
    {
        if (_atCars is { } previous && previous != Race.ArrowAtCars)
            _modeMessageUntil = _clock + 2;
        _atCars = Race.ArrowAtCars;
        var angle = Race.ArrowAngle ?? (Race.IsPreview ? 0f : (float?)null);
        if (angle is not { } target)
        {
            _hadArrow = false;
            return;
        }
        if (!_hadArrow) _arrowAngle = target;
        _hadArrow = true;
        _arrowAngle += Math.Clamp(target - _arrowAngle, -300 * dt, 300 * dt);
        var center = position.X + size.X / 2;
        var top = position.Y;
        if (Race.ArrowAtCars)
            Text($"[ {Race.ArrowTarget} ]", center, top + 3 * scale, scale, center: true, color: new Color(20, 20, 120));
        if (_clock < _modeMessageUntil)
            Text($"Arrow now pointing at {(Race.ArrowAtCars ? "Cars" : "Track")} <", center, top + 89 * scale, scale, center: true);
        if (!Race.IsPreview && !Race.ArrowAtCars && MathF.Abs(_arrowAngle) <= 7) return;

        // xtGraphics.arrow: rotate its seven X/Z vertices, then project at y=-90.
        ReadOnlySpan<float> xs = [0, -35, -15, -15, 15, 15, 35];
        ReadOnlySpan<float> zs = [110, 50, 50, -50, -50, 50, 50];
        Span<int> px = stackalloc int[7];
        Span<int> py = stackalloc int[7];
        var radians = _arrowAngle * (MathF.PI / 180);
        var sin = MathF.Sin(radians);
        var cos = MathF.Cos(radians);
        for (var i = 0; i < xs.Length; i++)
        {
            var x = xs[i] * cos + zs[i] * sin;
            var z = 700 - xs[i] * sin + zs[i] * cos;
            px[i] = (int)(center + x * 400 / z * scale);
            py[i] = (int)(top + (200 - 290 * 400 / z) * scale);
        }
        var turn = MathF.Abs(_arrowAngle);
        G.SetColor(Race.ArrowAtCars ? Snap(159, 207, 255) :
            Snap((int)(190 + 65 * Math.Clamp((turn - 90) / 50, 0, 1)), 255, 0));
        G.FillPolygon(px, py, 7);
        G.SetColor(Race.ArrowAtCars ? Snap(120, 114, 255) : Snap(115, 170, 0));
        G.SetStrokeWidth(MathF.Max(1, scale));
        G.DrawPolygon(px, py, 7);
    }

    private void PositionReadout(float x, float y, float scale)
    {
        const string label = "POSITION:";
        var valueFont = new Font(FontFamily.Adventure, FontStyle.Bold, 22 * scale);
        var baseline = y + G.GetFontMetrics(new Font(FontFamily.Adventure, FontStyle.Bold, 18 * scale)).LineHeight;
        G.SetFont(new Font(FontFamily.Adventure, FontStyle.Bold, 11 * scale));
        var labelWidth = G.GetFontMetrics().MeasureText(label).X;
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringOutlined(label, (int)x, (int)baseline, Snap(new Color(220, 245, 255, 170)), 0.5f * scale);
        G.SetFont(valueFont);
        var number = $"{Race.Position}";
        var numberX = x + labelWidth + 6 * scale;
        var numberWidth = G.GetFontMetrics().MeasureText(number).X;
        G.SetColor(Snap(25, 35, 145));
        G.DrawStringOutlined(number, (int)numberX, (int)(baseline + 8 * scale),
            Snap(new Color(220, 245, 255, 170)), 0.5f * scale);
        G.SetFont(new Font(FontFamily.Adventure, FontStyle.Bold, 9 * scale));
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringOutlined(OrdinalSuffix(Race.Position), (int)(numberX + numberWidth + 2 * scale),
            (int)(baseline - 5 * scale), Snap(new Color(220, 245, 255, 170)), 0.5f * scale);
    }

    private static Vector3 SnapMultiplier() => new(1 + World.Snap.R / 100f, 1 + World.Snap.G / 100f, 1 + World.Snap.B / 100f);

    private static Color Snap(Color color) => HudColorSnap.Apply(color, World.Snap);

    private static Color Snap(int r, int g, int b) => Snap(new Color(r, g, b));

    private static void Text(string text, float x, float y, float scale, bool adventure = false,
        bool center = false, Color? color = null)
    {
        G.SetFont(new Font(adventure ? FontFamily.Adventure : FontFamily.DroidSans, FontStyle.Bold, 11 * scale));
        G.SetColor(Snap(color ?? new Color(0, 0, 0)));
        var metrics = G.GetFontMetrics();
        if (center) x -= metrics.MeasureText(text).X / 2f;
        G.DrawStringOutlined(text, (int)x, (int)(y + metrics.LineHeight), Snap(new Color(220, 245, 255, 170)), 0.5f * scale);
    }

    private static void Digits(string text, float x, float y, float fontSize, bool center = false)
    {
        G.SetFont(new Font(FontFamily.Adventure, FontStyle.Bold, fontSize));
        G.SetColor(Snap(25, 35, 145));
        var metrics = G.GetFontMetrics();
        if (center) x -= metrics.MeasureText(text).X / 2;
        G.DrawStringOutlined(text, (int)x, (int)(y + metrics.LineHeight), Snap(220, 245, 255), fontSize / 18);
    }

    private static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    private static string Ordinal(int value) => $"{value}{OrdinalSuffix(value)}";
    private static string OrdinalSuffix(int value) => value % 100 is 11 or 12 or 13 ? "th"
        : (value % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
}
