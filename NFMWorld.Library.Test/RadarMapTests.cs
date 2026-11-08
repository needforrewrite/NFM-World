using System.Numerics;
using NFMWorld.UI.Hud;

namespace NFMWorld.Library.Test;

/// <summary>
/// The minimap draws a baked image of the stage with its own transform while the cars are drawn
/// as blips with another. If the two disagree the map is a picture the cars are not on, so the
/// property worth pinning down is that a point on the image lands exactly where the blip for that
/// world position does.
/// </summary>
[TestClass]
public class RadarMapTests
{
    private const float RadarX = 8f;
    private const float RadarY = 300f;
    private const float RadarSize = 164f;
    private const float RadarRange = 6000f;
    private const float UnitsPerPixel = 40f;
    private const float ImageSize = 512f;
    private const float MinX = -10_000f;
    private const float MinZ = -6_000f;
    private const float MaxZ = MinZ + ImageSize * UnitsPerPixel;

    private static (float Sin, float Cos) Heading(float radians) => (MathF.Sin(radians), MathF.Cos(radians));

    private static Vector2 Blip(float worldX, float worldZ, float clientX, float clientZ, float heading)
    {
        var (sin, cos) = Heading(heading);
        return RadarMap.ToRadar(
            worldX,
            worldZ,
            clientX,
            clientZ,
            sin,
            cos,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );
    }

    /// <summary>
    /// The screen point the image's pixel (u, v) draws at, and the blip for that same world spot.
    /// </summary>
    private static (Vector2 Image, Vector2 Blip) Sample(float u, float v, float clientX, float clientZ, float heading)
    {
        var placement = RadarMap.Place(
            MinX,
            MaxZ,
            UnitsPerPixel,
            clientX,
            clientZ,
            heading,
            RadarX,
            RadarY,
            RadarSize,
            RadarRange
        );

        // v runs down the image, which its bake lays along -Z.
        var image = RadarMap.PixelToScreen(placement, u, v);
        var blip = Blip(MinX + u * UnitsPerPixel, MaxZ - v * UnitsPerPixel, clientX, clientZ, heading);
        return (image, blip);
    }

    [TestMethod]
    public void Place_CarAtImageCorner_ImageCornerLandsOnRadarCentre()
    {
        // With the car exactly at the image's top-left world point and no rotation, that corner is
        // the car, so it must be dead centre of the radar.
        var (image, blip) = Sample(0, 0, MinX, MaxZ, 0f);

        Assert.AreEqual(RadarX + RadarSize / 2, image.X, 0.01f);
        Assert.AreEqual(RadarY + RadarSize / 2, image.Y, 0.01f);
        Assert.AreEqual(image.X, blip.X, 0.001f);
        Assert.AreEqual(image.Y, blip.Y, 0.001f);
    }

    [TestMethod]
    public void Place_NoRotation_MapsImageAxesToScreenAxes()
    {
        // Heading 0 faces +Z, which draws UP the radar. u runs right along +X; v runs down the
        // image, which is -Z, so it draws down the screen (a positive Y step).
        var placement = RadarMap.Place(
            MinX,
            MaxZ,
            UnitsPerPixel,
            0f,
            0f,
            0f,
            RadarX,
            RadarY,
            RadarSize,
            RadarRange
        );

        Assert.AreEqual(0f, placement.AxisX.Y, 1e-4f);
        Assert.IsGreaterThan(0f, placement.AxisX.X);
        Assert.AreEqual(0f, placement.AxisY.X, 1e-4f);
        Assert.IsGreaterThan(0f, placement.AxisY.Y);
    }

    [TestMethod]
    public void Place_QuarterTurn_PutsWorldForwardUpTheRadar()
    {
        // Heading 90°: the car's forward is up the radar, so an image pixel ahead of the car must
        // land above the centre and one to its right must land to the right.
        var (ahead, aheadBlip) = Sample(100, 0, 0f, 0f, MathF.PI / 2);
        var (right, rightBlip) = Sample(200, 0, 0f, 0f, MathF.PI / 2);

        Assert.AreEqual(aheadBlip.X, ahead.X, 0.001f);
        Assert.AreEqual(aheadBlip.Y, ahead.Y, 0.001f);
        Assert.AreEqual(rightBlip.X, right.X, 0.001f);
        Assert.AreEqual(rightBlip.Y, right.Y, 0.001f);
    }

    [TestMethod]
    public void Place_AnySampleOnTheImage_MatchesItsBlip()
    {
        // The general property, over a spread of points and headings.
        foreach (var heading in new[] { 0f, 0.3f, MathF.PI / 2, 2.1f, MathF.PI, -1.4f })
        foreach (var (clientX, clientZ) in new[] { (0f, 0f), (MinX, MaxZ), (4_321f, -987f) })
        foreach (var (u, v) in new[] { (0f, 0f), (ImageSize, 0f), (0f, ImageSize), (ImageSize, ImageSize), (137f, 411f) })
        {
            var (image, blip) = Sample(u, v, clientX, clientZ, heading);
            Assert.AreEqual(blip.X, image.X, 0.01f, $"x at u={u} v={v} heading={heading} client=({clientX},{clientZ})");
            Assert.AreEqual(blip.Y, image.Y, 0.01f, $"y at u={u} v={v} heading={heading} client=({clientX},{clientZ})");
        }
    }

    [TestMethod]
    public void Place_ForwardIsUp_AndRightIsRight()
    {
        // The property the map/blip tests cannot catch, because both transforms could be inverted
        // together: the radar must put the car's forward direction at the TOP. At heading 0 the
        // car faces +Z (FollowCamera sits behind it at -Z), so +Z has to draw above the centre -
        // phy's radarstat subtracts dz for exactly this reason. A car to the right (+X) draws right.
        var placement = RadarMap.Place(
            MinX,
            MaxZ,
            UnitsPerPixel,
            0f,
            0f,
            0f,
            RadarX,
            RadarY,
            RadarSize,
            RadarRange
        );
        var center = RadarMap.ToRadar(
            0f,
            0f,
            0f,
            0f,
            0f,
            1f,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );

        var ahead = RadarMap.ToRadar(
            0f,
            1_000f,
            0f,
            0f,
            0f,
            1f,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );
        var right = RadarMap.ToRadar(
            1_000f,
            0f,
            0f,
            0f,
            0f,
            1f,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );

        Assert.IsLessThan(center.Y, ahead.Y, "a point ahead (+Z) must draw above the car");
        Assert.AreEqual(center.X, ahead.X, 0.01f, "a point straight ahead stays on the car's axis");
        Assert.IsGreaterThan(center.X, right.X, "a point to the right (+X) must draw to the right");

        // Forward is (-sin, cos), so a quarter turn points the car along -X: -X draws straight
        // ahead (up, on the car's axis) and +Z draws to its right.
        var center2 = RadarMap.ToRadar(
            0f,
            0f,
            0f,
            0f,
            1f,
            0f,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );
        var ahead2 = RadarMap.ToRadar(
            -1_000f,
            0f,
            0f,
            0f,
            1f,
            0f,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );
        var right2 = RadarMap.ToRadar(
            0f,
            1_000f,
            0f,
            0f,
            1f,
            0f,
            RadarX + RadarSize / 2,
            RadarY + RadarSize / 2,
            RadarSize,
            RadarRange
        );
        Assert.IsLessThan(center2.Y, ahead2.Y, "facing -X, -X draws ahead (up)");
        Assert.AreEqual(center2.X, ahead2.X, 0.01f, "facing -X, -X stays on the car's axis");
        Assert.IsGreaterThan(center2.X, right2.X, "facing -X, +Z draws to the right");

        // And the image's own axes have to say the same thing, or the track under the car is not
        // the track the blips sit on. v runs down the image, i.e. towards -Z, which is also down
        // the screen; u runs right along +X.
        var imageTop = RadarMap.PixelToScreen(placement, 0f, (MaxZ - 0f) / UnitsPerPixel);
        var imageDown = RadarMap.PixelToScreen(placement, 0f, (MaxZ - (-1_000f)) / UnitsPerPixel);
        Assert.IsGreaterThan(imageTop.Y, imageDown.Y, "the image's +v axis must run down the radar");
        var imageLeft = RadarMap.PixelToScreen(placement, (MinX - MinX) / UnitsPerPixel, 0f);
        var imageRight = RadarMap.PixelToScreen(placement, (MinX + 1_000f - MinX) / UnitsPerPixel, 0f);
        Assert.IsGreaterThan(imageLeft.X, imageRight.X, "the image's +u axis must run right");
    }

    [TestMethod]
    public void Place_PixelStepMatchesOnePixelOfWorld()
    {
        // Rendering the image at the wrong scale would show a course smaller or larger than the
        // one the blips sit on, so one pixel must be worth exactly unitsPerPixel world units.
        var placement = RadarMap.Place(
            MinX,
            MaxZ,
            UnitsPerPixel,
            0f,
            0f,
            0f,
            RadarX,
            RadarY,
            RadarSize,
            RadarRange
        );

        var expected = UnitsPerPixel * RadarSize * 0.5f / RadarRange;
        Assert.AreEqual(expected, placement.AxisX.Length(), 1e-3f);
        Assert.AreEqual(expected, placement.AxisY.Length(), 1e-3f);
    }

    [TestMethod]
    public void ToUnitSquare_DrawsTheWholeImageWhereItsPixelsLand()
    {
        // The renderer maps the unit square, but Place works in image pixels. Drawing the raw
        // placement therefore puts the 512 px image into a 2.4 px quad - invisible. Converting
        // must keep every corner on the spot its own pixel draws at.
        var placement = RadarMap.Place(
            MinX,
            MaxZ,
            UnitsPerPixel,
            0f,
            0f,
            0.7f,
            RadarX,
            RadarY,
            RadarSize,
            RadarRange
        );
        var unit = RadarMap.ToUnitSquare(placement, ImageSize, ImageSize);

        foreach (var (u, v) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
        {
            var fromUnit = unit.Origin + u * unit.AxisX + v * unit.AxisY;
            var fromPixels = RadarMap.PixelToScreen(placement, u * ImageSize, v * ImageSize);
            Assert.AreEqual(fromPixels.X, fromUnit.X, 0.01f);
            Assert.AreEqual(fromPixels.Y, fromUnit.Y, 0.01f);
        }

        // Four square corners: the image stays square, not squashed.
        Assert.AreEqual(unit.AxisX.Length(), unit.AxisY.Length(), 1e-3f);
        Assert.IsGreaterThan(100f, unit.AxisX.Length());
        Assert.AreEqual(0f, Vector2.Dot(Vector2.Normalize(unit.AxisX), Vector2.Normalize(unit.AxisY)), 1e-4f);
    }
}
