using NFMWorld.UI.Hud;
using NFMWorldLibrary.Hud;

namespace NFMWorld.Library.Test;

[TestClass]
public class RadarPolygonIndexTests
{
    [TestMethod]
    public void Query_CullsDistantSurfacesAndPreservesPaintOrder()
    {
        var polygons = new List<MinimapPolygon>();

        for (var i = 0; i < 100; i++)
        {
            var x = (99 - i) * 1000f;
            polygons.Add(new MinimapPolygon([(x, 0), (x + 100, 0), (x, 100)], 0, 0, 0));
        }

        // This large polygon crosses the query although all its vertices lie outside it.
        polygons.Add(new MinimapPolygon([(-1000, -1000), (100000, -1000), (0, 100000)], 0, 0, 0));
        var index = new RadarPolygonIndex(polygons);

        CollectionAssert.AreEqual(new[] { 49, 50, 51, 100 }, index.Query(48000, -10, 50000, 10).ToArray());
        CollectionAssert.AreEqual(new[] { 100 }, index.Query(-500, -500, -400, -400).ToArray());
        Assert.AreEqual(0, index.Query(-5000, -5000, -4000, -4000).Length);
    }
}
