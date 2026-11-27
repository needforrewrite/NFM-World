using System.Numerics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.GadgetSkin;

/// <summary>
/// The radar every gadget skin shares: the rotating map (stage polygons or checkpoint loop)
/// with one oriented chevron per racer. phy drew it into a clipped frame; the skin owns the frame
/// and passes the radar's screen centre and side, so the same body serves every variant.
/// </summary>
internal static class GadgetRadar
{
    public const float Range = 6000;

    public static void Draw(GadgetHud hud, IHudData race, Vector2 center, float screenSize)
    {
        if (race.ClientPosition is not { } client) return;
        var sin = MathF.Sin(race.ClientHeading);
        var cos = MathF.Cos(race.ClientHeading);

        Vector2 ToRadar(float worldX, float worldZ)
            => RadarMap.ToRadar(worldX, worldZ, client.X, client.Z, sin, cos, center.X, center.Y, screenSize, Range);

        if (!RadarPolygons.Draw(
            race.MapPolygons,
            client.X,
            client.Z,
            sin,
            cos,
                center.X,
            center.Y,
            screenSize,
            Range
        ))
        {
            var line = GadgetHud.Snap(190, 220, 245, 180);
            var checkpoints = race.Checkpoints;
            for (var i = 0; i < checkpoints.Count; i++)
            {
                var a = ToRadar(checkpoints[i].X, checkpoints[i].Z);
                var b = ToRadar(checkpoints[(i + 1) % checkpoints.Count].X, checkpoints[(i + 1) % checkpoints.Count].Z);
                hud.ScreenLine(line, a, b, 1.5f);
            }
        }

        if (race.TargetCheckpoint is { } target)
        {
            var p = ToRadar(target.X, target.Z);
            hud.ScreenOval(GadgetHud.Snap(255, 210, 60, 230), p, 3);
        }

        foreach (var marker in race.Markers)
        {
            if (marker.Wasted || marker.IsClient) continue;
            var p = ToRadar(marker.X, marker.Z);
            hud.ScreenIcon(p, GadgetHud.Snap(159, 80, 0), GadgetHud.Snap(50, 50, 50));
        }

        // The client, dead centre and nose up.
        hud.ScreenIcon(center, GadgetHud.Snap(50, 204, 255), GadgetHud.Snap(50, 50, 50));
    }
}
