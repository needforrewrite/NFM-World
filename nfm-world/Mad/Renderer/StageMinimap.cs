using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Hud;
using NFMWorldLibrary.Util;

namespace NFMWorld;

/// <summary>Extracts a stage's colored surfaces for the top-down minimap.</summary>
public static class StageMinimap
{
    public static IReadOnlyList<MinimapPolygon> Extract(BackendStage stage)
    {
        var surfaces = new List<(float Height, MinimapPolygon Polygon)>();
        foreach (var piece in stage.Pieces.OfType<StageObject>())
        {
            var world = Matrix.CreateFromEuler((Euler)piece.Rotation)
                        * Matrix.CreateTranslation((Vector3)piece.Position);
            foreach (var poly in piece.Rad.Polys)
            {
                var color = poly.Color;
                var triangles = poly.Triangles;
                for (var i = 0; i + 2 < triangles.Length; i += 3)
                {
                    var a = Vector3.Transform(poly.Points[(int)triangles[i]], world);
                    var b = Vector3.Transform(poly.Points[(int)triangles[i + 1]], world);
                    var c = Vector3.Transform(poly.Points[(int)triangles[i + 2]], world);
                    var area = ((double)b.X - a.X) * (c.Z - a.Z) - ((double)b.Z - a.Z) * (c.X - a.X);
                    if (Math.Abs(area) < 1e-3) continue;

                    surfaces.Add(((a.Y + b.Y + c.Y) / 3, new MinimapPolygon(
                        [(a.X, a.Z), (b.X, b.Z), (c.X, c.Z)],
                        (byte)Math.Clamp((int)color.R, 0, 255),
                        (byte)Math.Clamp((int)color.G, 0, 255),
                        (byte)Math.Clamp((int)color.B, 0, 255)
                    ))
                    );
                }
            }
        }

        // The game's vertical axis points down, so higher Y surfaces go behind lower ones.
        return surfaces.OrderByDescending(surface => surface.Height)
            .Select(surface => surface.Polygon).ToArray();
    }
}
