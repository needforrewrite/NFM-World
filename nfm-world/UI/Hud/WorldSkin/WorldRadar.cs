using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.WorldSkin;

/// <summary>Circular radar: the baked stage (or checkpoint loop) rotated into the client car's frame, with racers as blips.</summary>
internal sealed class WorldRadar : Component
{
    public const float Size = 164;
    private const float Range = 6000;

    private readonly IHudData _race;

    public WorldRadar(IHudData race)
    {
        _race = race;
        Styles = Sx.Styles(width: Size, height: Size, flexShrink: 0, pointerEvents: false);
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var w = MathF.Min(size.X, size.Y);
        if (w <= 0) return;
        var scale = w / Size;

        G.SaveState();
        try
        {
            G.IntersectScissor(position.X, position.Y, w, w, w / 2);
            G.SetColor(new Color(3, 8, 20, 120));
            G.FillOval((int)position.X, (int)position.Y, (int)w, (int)w);

            if (_race.ClientPosition is { } client)
                DrawContents(client, position.X, position.Y, w, scale);

            G.ResetScissor();
            G.SetColor(Theme.MenuBorder);
            G.SetStrokeWidth(MathF.Max(1, scale));
            G.DrawOval((int)position.X, (int)position.Y, (int)w, (int)w);
        }
        finally
        {
            G.RestoreState();
        }
    }

    private void DrawContents((float X, float Y, float Z) client, float x, float y, float w, float scale)
    {
        var sin = MathF.Sin(_race.ClientHeading);
        var cos = MathF.Cos(_race.ClientHeading);
        var center = w / 2;

        Vector2 ToRadar(float worldX, float worldZ)
            => RadarMap.ToRadar(worldX, worldZ, client.X, client.Z, sin, cos, center, center, w, Range);

        if (!DrawMap(client.X, client.Z, x, y, w))
        {
            G.SetColor(new Color(185, 211, 255, 150));
            G.SetStrokeWidth(MathF.Max(1, 1.5f * scale));
            var checkpoints = _race.Checkpoints;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var a = ToRadar(checkpoints[i].X, checkpoints[i].Z);
                var b = ToRadar(checkpoints[(i + 1) % checkpoints.Count].X, checkpoints[(i + 1) % checkpoints.Count].Z);
                G.DrawLine((int)(x + a.X), (int)(y + a.Y), (int)(x + b.X), (int)(y + b.Y));
            }
        }

        if (_race.TargetCheckpoint is { } target)
        {
            var p = ToRadar(target.X, target.Z);
            G.SetColor(Theme.Warning);
            var r = 3.5f * scale;
            G.FillOval((int)(x + p.X - r), (int)(y + p.Y - r), (int)(r * 2), (int)(r * 2));
        }

        foreach (var marker in _race.Markers)
        {
            if (marker.Wasted || marker.IsClient) continue;
            var p = ToRadar(marker.X, marker.Z);
            G.SetColor(new Color(244, 90, 70));
            var r = 3 * scale;
            G.FillOval((int)(x + p.X - r), (int)(y + p.Y - r), (int)(r * 2), (int)(r * 2));
        }

        // The client is always dead centre, nose up.
        Span<int> px = stackalloc int[3];
        Span<int> py = stackalloc int[3];
        var cx = x + center;
        var cy = y + center;
        px[0] = (int)cx; py[0] = (int)(cy - 7 * scale);
        px[1] = (int)(cx - 5 * scale); py[1] = (int)(cy + 5 * scale);
        px[2] = (int)(cx + 5 * scale); py[2] = (int)(cy + 5 * scale);
        G.SetColor(Theme.MenuBlue);
        G.FillPolygon(px, py, 3);
        G.SetColor(Theme.TextPrimary);
        G.SetStrokeWidth(MathF.Max(1, scale));
        G.DrawPolygon(px, py, 3);
    }

    private bool DrawMap(float clientX, float clientZ, float x, float y, float size)
    {
        if (_race.MapImage is not IImage image) return false;
        var (minX, maxZ, unitsPerPixel) = _race.MapFrame;
        if (unitsPerPixel <= 0f) return false;

        var placement = RadarMap.ToUnitSquare(
            RadarMap.Place(minX, maxZ, unitsPerPixel, clientX, clientZ, _race.ClientHeading, x, y, size, Range),
            image.Width, image.Height);
        G.DrawImageTransformed(image, placement.Origin, placement.AxisX, placement.AxisY, new Color(255, 255, 255, 200));
        return true;
    }
}
