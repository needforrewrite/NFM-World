using System.Numerics;

namespace NFMWorld.UI.Hud;

/// <summary>Where the radar draws the baked stage image: the parallelogram <c>Origin + u*AxisX + v*AxisY</c>.</summary>
public readonly record struct RadarMapPlacement(Vector2 Origin, Vector2 AxisX, Vector2 AxisY);

/// <summary>
/// Places the baked top-down stage image on the rotating radar, so the real track turns with the
/// car exactly as its blips do. Kept apart from the HUD so the placement can be checked against
/// the blip transform, which is the property that matters: a map that rotates correctly but is
/// offset, or at a different scale, reads as a track the cars are not on.
/// </summary>
public static class RadarMap
{
    /// <summary>
    /// Places the image. The radar centres on the client car and rotates by its heading, so the
    /// car's forward direction is up.
    /// </summary>
    /// <param name="minX">World X of the image's left edge.</param>
    /// <param name="maxZ">World Z of the image's top edge (its larger Z; see StageMinimap).</param>
    /// <param name="unitsPerPixel">World units one image pixel covers on both axes.</param>
    /// <param name="clientX">Client car's world X.</param>
    /// <param name="clientZ">Client car's world Z.</param>
    /// <param name="heading">Car heading in radians, as the radar's blips use it.</param>
    /// <param name="radarX">Radar square's left edge on screen.</param>
    /// <param name="radarY">Radar square's top edge on screen.</param>
    /// <param name="radarSize">Radar square's side, in screen pixels.</param>
    /// <param name="radarRange">World units from the car to the radar's edge.</param>
    public static RadarMapPlacement Place(
        float minX,
        float maxZ,
        float unitsPerPixel,
        float clientX,
        float clientZ,
        float heading,
        float radarX,
        float radarY,
        float radarSize,
        float radarRange
    )
    {
        var sin = MathF.Sin(heading);
        var cos = MathF.Cos(heading);

        // World offset -> radar pixels, then rotated so forward is up. This is the transform the
        // blips use, and the image has to land through the same one.
        var k = radarSize * 0.5f / radarRange;
        var step = k * unitsPerPixel;

        // The image's top-left pixel is (minX, maxZ), so that is the corner the placement starts at.
        var origin = ToRadar(
            minX,
            maxZ,
            clientX,
            clientZ,
            sin,
            cos,
            radarX + radarSize * 0.5f,
            radarY + radarSize * 0.5f,
            radarSize,
            radarRange
        );

        // The image's own axes are +X for u and -Z for v (StageMinimap.Bounds), and the radar
        // screen is the car's frame: right is (cos, sin) and forward is (-sin, cos), drawn upward
        // because screen Y grows downward. Same rotation as ToRadar, so the map turns with the blips.
        return new RadarMapPlacement(
            origin,
            new Vector2(step * cos, step * sin),
            new Vector2(-step * sin, step * cos)
        );
    }

    /// <summary>The screen point a pixel of the placed image draws at.</summary>
    public static Vector2 PixelToScreen(in RadarMapPlacement placement, float u, float v)
        => placement.Origin + u * placement.AxisX + v * placement.AxisY;

    /// <summary>
    /// The same placement in the unit square the renderer's matrix maps: with these axes,
    /// <c>Origin + u*AxisX + v*AxisY</c> for u, v in [0,1] covers the whole image. <see cref="Place"/>
    /// works in image pixels, so handing its axes straight to a unit-square draw shrinks the
    /// image to one pixel.
    /// </summary>
    public static RadarMapPlacement ToUnitSquare(in RadarMapPlacement placement, float width, float height)
        => new(placement.Origin, placement.AxisX * width, placement.AxisY * height);

    /// <summary>The blip transform: world XZ to radar pixels, rotated so the car faces up.</summary>
    public static Vector2 ToRadar(
        float x,
        float z,
        float clientX,
        float clientZ,
        float sin,
        float cos,
        float centerX,
        float centerY,
        float size,
        float radarRange
    )
    {
        // Rotate into the car's frame: car right is (cos, sin) and car forward is (-sin, cos).
        // Screen Y grows downward, so forward comes out as a negative Y step and draws up the
        // radar. This is the sign phy's radarstat uses (141 - dz/prox); getting it backwards
        // still leaves the map and blips agreeing with each other, so only a test of the
        // direction itself catches it.
        var dx = (x - clientX) / radarRange * (size / 2);
        var dz = (z - clientZ) / radarRange * (size / 2);
        return new Vector2(centerX + dx * cos + dz * sin, centerY + dx * sin - dz * cos);
    }
}
