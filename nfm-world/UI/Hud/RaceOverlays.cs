using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

/// <summary>
/// Race overlays drawn outside any HUD skin: name tags over each car, the minimap and the
/// opponent panel. phy-addons' player tags / radar / opponent status, as screen-space draws.
/// </summary>
public sealed class RaceOverlays : Component
{
    private const float RadarSize = 164;
    private const float RadarRange = 6000;

    /// <summary>Radar corner radius in unscaled pixels; scales with the HUD.</summary>
    private const float CornerRadius = 10;

    private readonly IHudData _race;

    public RaceOverlays(IHudData race)
    {
        _race = race;
        Styles = Sx.Styles(position: Position.Absolute, left: 0, top: 0,
            width: "100%", height: "100%", flexShrink: 0, pointerEvents: false);
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        if (_race.IsPreview || _race.Paused) return;
        G.SaveState();
        try
        {
            if (SettingsMenu.HudRadar) DrawRadar(position, size);
            // The standings panel belongs to the skin: ClassicHud and JSON skins each draw their
            // own, so one here would be a second list beside theirs.
            if (SettingsMenu.HudNameTags) DrawNameTags(position, size);
        }
        finally
        {
            G.RestoreState();
        }
    }

    /// <summary>
    /// Track outline rotated into the client car's frame, with every racer as a blip.
    /// Mirrors <c>GadgetPainter.radarstat</c>.
    /// </summary>
    private void DrawRadar(Vector2 position, Vector2 size)
    {
        if (_race.ClientPosition is not { } client) return;
        var scale = MathF.Min(size.X / 960, size.Y / 540) * HudScaleSettings.Default * SettingsMenu.HudScale;
        if (scale <= 0) return;
        var bounds = HudSafeZone.Bounds(position, size);
        var x = bounds.X + 8 * scale;
        var y = bounds.Y + bounds.Height - RadarSize * scale - 8 * scale;
        var w = RadarSize * scale;

        var centerX = w / 2;
        var centerY = w / 2;
        // The car's forward direction is (-sin θ, cos θ); the camera sits behind it at
        // (sin θ, -cos θ), so rotating the world by θ puts forward up on the radar.
        var sin = MathF.Sin(_race.ClientHeading);
        var cos = MathF.Cos(_race.ClientHeading);

        // No background fill: the map is the whole radar, and the clip below rounds it to the
        // frame so its square corners do not poke outside the rounded border.
        var corner = CornerRadius * scale;
        G.IntersectScissor(x, y, w, w, corner);

        if (!DrawMap(client.X, client.Z, x, y, w))
        {
            // No baked map yet: the connecting line between checkpoints, closed into a loop.
            G.SetColor(new Color(190, 220, 245, 180));
            G.SetStrokeWidth(MathF.Max(1, 1.5f * scale));
            var checkpoints = _race.Checkpoints;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var a = checkpoints[i];
                var b = checkpoints[(i + 1) % checkpoints.Count];
                var pa = ToRadar(a.X, a.Z, client.X, client.Z, sin, cos, centerX, centerY, w);
                var pb = ToRadar(b.X, b.Z, client.X, client.Z, sin, cos, centerX, centerY, w);
                G.DrawLine((int)(x + pa.X), (int)(y + pa.Y), (int)(x + pb.X), (int)(y + pb.Y));
            }
        }

        // Checkpoint the player is heading for.
        if (_race.TargetCheckpoint is { } checkpoint)
        {
            var p = ToRadar(checkpoint.X, checkpoint.Z, client.X, client.Z, sin, cos, centerX, centerY, w);
            G.SetColor(new Color(255, 210, 60, 230));
            G.FillOval((int)(x + p.X - 3 * scale), (int)(y + p.Y - 3 * scale), (int)(6 * scale), (int)(6 * scale));
        }

        foreach (var marker in _race.Markers)
        {
            if (marker.Wasted) continue;
            var p = ToRadar(marker.X, marker.Z, client.X, client.Z, sin, cos, centerX, centerY, w);
            if (marker.IsClient) G.SetColor(new Color(90, 195, 255, 255));
            else G.SetColor(new Color(244, 90, 70, 255));
            var r = (marker.IsClient ? 4 : 3) * scale;
            G.FillOval((int)(x + p.X - r), (int)(y + p.Y - r), (int)(r * 2), (int)(r * 2));
        }

        G.SetColor(new Color(220, 240, 255, 90));
        G.SetStrokeWidth(MathF.Max(1, scale));
        G.DrawRoundedRect((int)x, (int)y, (int)w, (int)w, corner, corner, corner, corner);
        G.ResetScissor();
    }

    /// <summary>Project each visible racer's roof to the screen and label it.</summary>
    private void DrawNameTags(Vector2 position, Vector2 size)
    {
        if (_race.Racers.Count <= 1 || _race.ClientPosition is null) return;
        var scale = MathF.Min(size.X / 960, size.Y / 540) * HudScaleSettings.Default * SettingsMenu.HudScale;
        if (scale <= 0) return;
        G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Bold, 11 * scale));
        var metrics = G.GetFontMetrics();

        foreach (var marker in _race.Markers)
        {
            if (marker.IsClient || marker.Wasted) continue;
            if (!Project(new Vector3(marker.X, marker.Y - 120, marker.Z), out var screen, position, size)) continue;
            var width = metrics.MeasureText(marker.Name).X;
            var height = metrics.LineHeight;
            var x = screen.X - width / 2;
            G.SetColor(new Color(0, 0, 0, 110));
            G.FillRoundedRect((int)(x - 4 * scale), (int)(screen.Y - 2 * scale),
                (int)(width + 8 * scale), (int)(height + 4 * scale), 4, 4, 4, 4);
            G.SetColor(new Color(235, 245, 255));
            G.DrawString(marker.Name, (int)x, (int)screen.Y + (int)metrics.LineHeight);
        }
    }

    private bool Project(Vector3 world, out Vector2 screen, Vector2 position, Vector2 size)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), _race.ViewProjection);
        screen = default;
        if (clip.W <= 0.001f) return false;
        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;
        if (ndcX is < -1 or > 1 || ndcY is < -1 or > 1) return false;
        screen = position + new Vector2((ndcX + 1f) * 0.5f * size.X, (1f - ndcY) * 0.5f * size.Y);
        return true;
    }

    /// <summary>
    /// Draws the baked stage image rotated so the car's heading points up, and clipped to the
    /// radar square. Returns false when there is no image yet, leaving the caller the line map.
    /// </summary>
    private bool DrawMap(float clientX, float clientZ, float x, float y, float size)
    {
        if (_race.MapImage is not NFMWorld.DriverInterface.DriverInterface.IImage image)
            return false;

        var (minX, maxZ, unitsPerPixel) = _race.MapFrame;
        if (unitsPerPixel <= 0f)
            return false;

        // The placement is shared with the blips' transform (RadarMap), so the image cannot drift
        // from the markers drawn on top of it. Its axes are per image pixel; the draw maps the
        // unit square, so they scale by the image's size.
        var placement = RadarMap.ToUnitSquare(
            RadarMap.Place(minX, maxZ, unitsPerPixel, clientX, clientZ, _race.ClientHeading,
                x, y, size, RadarRange),
            image.Width, image.Height);

        G.DrawImageTransformed(image, placement.Origin, placement.AxisX, placement.AxisY, new Color(255, 255, 255, 200));
        return true;
    }

    // World XZ offset from the car, scaled into the radar, then rotated by the car's heading so
    // forward maps to the top of the map. Shared with the map image's own placement.
    private static Vector2 ToRadar(float x, float z, float clientX, float clientZ,
        float sin, float cos, float centerX, float centerY, float size)
        => RadarMap.ToRadar(x, z, clientX, clientZ, sin, cos, centerX, centerY, size, RadarRange);
}
