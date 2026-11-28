using System.Numerics;
using FixedMathSharp;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

[TestClass, DoNotParallelize]
public class CarWheelsTests
{
    private const string LayeredWheels = """
                                         rims(50,50,50,17,20)
                                         w(-39,2,72,11,17,0)
                                         w(39,2,72,11,-17,0)
                                         rims(50,50,50,17,20)
                                         w(-39,2,-84,0,17,0)
                                         w(39,2,-84,0,-17,0)
                                         rims(215,190,105,21,-20)
                                         w(-42,2,72,11,17,12)
                                         w(42,2,72,11,-17,12)
                                         rims(215,190,105,21,-20)
                                         w(-42,2,-84,0,17,12)
                                         w(42,2,-84,0,-17,12)
                                         """;

    [TestMethod]
    public void LayeredWheels_UseOuterTiresForAllFourContacts()
    {
        var rad = RadParser.ParseRad(LayeredWheels);
        var car = new BackendCar(rad, 0, 0, 0, true);
        var contact = new ContO(car);

        Assert.AreEqual(8, car.Wheels.Count);
        Assert.AreEqual(4, car.CollisionWheels.Count);
        CollectionAssert.AreEqual(
            new[] { -42, 42, -42, 42 },
            new[] { contact.Keyx[0], contact.Keyx[1], contact.Keyx[2], contact.Keyx[3] }
        );
        Assert.AreEqual(rad.Wheels[4].Ground, car.GroundAt);
    }

    [TestMethod]
    public void LayeredWheels_RimSetsSurviveSerializationAndCanBeRecoloredIndependently()
    {
        var rad = RadParser.ParseRad(LayeredWheels);
        var encoded = TightPack.TightPackSerializer.Serialize(rad.Wheels[4]);
        var decoded = TightPack.TightPackSerializer.Deserialize<Rad3dWheelDef>(encoded);
        Assert.AreEqual(rad.Wheels[4].Rims, decoded.Rims);
        var stockColors = CarWheels.RimSlots(rad);
        Assert.AreEqual(2, stockColors.Count);
        Assert.AreEqual(new Color3(50, 50, 50), stockColors[0].Color);
        Assert.AreEqual(new Color3(215, 190, 105), stockColors[1].Color);

        var chosen = new Color3(30, 60, 220);
        var painted = CarPaint.CreateInstance(rad, wheels: new WheelAppearance { RimColors = [null, chosen] });
        var paint = CarWheels.RimSlots(painted);
        Assert.AreEqual(stockColors[0], paint[0]);
        Assert.AreEqual(chosen, paint[1].Color);
        Assert.AreEqual(-20f, painted.Wheels[4].Rims!.Value.Depth);
        CarWheels.Apply(painted, null);
        CollectionAssert.AreEqual(stockColors.ToArray(), CarWheels.RimSlots(painted).ToArray());
    }

    [TestMethod]
    public void WheelPaintIsIndividualAndDoesNotTouchBodyOrUntaggedPolygons()
    {
        var stock = Model();
        var chosen = new Color3(200, 40, 60);
        var local = CarPaint.CreateInstance(stock, wheels: new WheelAppearance { Colors = [chosen] });
        var bot = CarPaint.CreateInstance(local);
        var remote = CarPaint.CreateInstance(stock, wheels: new WheelAppearance
        {
            Colors = [new Color3(30, 180, 90)]
        });
        Assert.AreEqual(chosen, local.Wheels[0].Polys![0].Color);
        Assert.AreEqual(stock.Wheels[0].Polys![1].Color, local.Wheels[0].Polys![1].Color);
        Assert.AreEqual(stock.Polys[0].Color, local.Polys[0].Color);
        Assert.AreEqual(stock.Wheels[0].Polys![0].Color, bot.Wheels[0].Polys![0].Color);
        Assert.AreEqual(new Color3(30, 180, 90), remote.Wheels[0].Polys![0].Color);
        Assert.AreNotSame(stock.Wheels[0].Polys, local.Wheels[0].Polys);
    }

    [TestMethod]
    public void OriginalWheelsKeepPhysicsDimensionsAndCanRestoreTheEmbeddedMesh()
    {
        var stock = Model();
        var preview = CarPaint.CreateInstance(stock);
        CarWheels.Apply(preview, new WheelAppearance { OriginalWheels = true, RimColor = new Color3(0, 10, 255) });
        Assert.IsNull(preview.Wheels[0].Polys);
        Assert.AreEqual(stock.Wheels[0].Position, preview.Wheels[0].Position);
        Assert.AreEqual(stock.Wheels[0].Width, preview.Wheels[0].Width);
        Assert.AreEqual(stock.Wheels[0].Height, preview.Wheels[0].Height);
        Assert.AreEqual(stock.Wheels[0].Rotates, preview.Wheels[0].Rotates);
        Assert.AreEqual(new Color3(15, 15, 255), preview.Rims!.Value.Color);
        CarWheels.Apply(preview, null);
        Assert.IsNotNull(preview.Wheels[0].Polys);
        CollectionAssert.AreEqual(stock.Wheels[0].Polys!, preview.Wheels[0].Polys!);
        Assert.AreEqual(stock.Rims, preview.Rims);
    }

    [TestMethod]
    public void PreferencesSurviveReloadAndAreReturnedAsIndependentCopies()
    {
        var root = Path.Combine(Path.GetTempPath(), "NFMWorld-wheels-" + Guid.NewGuid().ToString("N"));

        try
        {
            var fs = new VirtualFS();
            fs.MountWriteDestination(root);
            CarWheels.Load(fs);
            CarWheels.Set("nfmm/test", new WheelAppearance
            {
                OriginalWheels = true, RimColor = new Color3(40, 50, 60), Colors = [new Color3(0, 255, 0)]
            }, fs);
            CarWheels.Flush(fs);
            CarWheels.Load(fs);
            var style = CarWheels.Get("nfmm/test");
            Assert.IsTrue(style.OriginalWheels);
            Assert.AreEqual(new Color3(40, 50, 60), style.RimColor!.Value);
            Assert.AreEqual(new Color3(15, 255, 15), style.Colors![0]!.Value);
            style.Colors[0] = null;
            style.OriginalWheels = false;
            Assert.IsNotNull(CarWheels.Get("nfmm/test").Colors![0]);
            Assert.IsTrue(CarWheels.Get("nfmm/test").OriginalWheels);
        }
        finally
        {
            CarWheels.Load(new VirtualFS());
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void CustomWheelsRequireTheirOwnColorDeclarations()
    {
        const string definition = """
                                  1stColor(120,120,120)
                                  <wheel>
                                  <p>
                                  c(120,120,120)
                                  p(0,0,0)
                                  p(0,10,0)
                                  p(0,0,10)
                                  </p>
                                  </wheel>
                                  w(40,20,60,11,20,20)
                                  """;
        var undeclared = RadParser.ParseRad(definition);
        Assert.IsNull(undeclared.Wheels[0].Polys![0].ColNum);
        var declared = RadParser.ParseRad("1stWheelColor(120,120,120)\n" + definition);
        Assert.AreEqual(0, declared.Wheels[0].Polys![0].ColNum);
    }

    private static Rad3d Model()
    {
        var poly = new Rad3dPoly(
            new Color3(100, 110, 120),
            0,
            PolyType.Flat,
            null,
            0,
            [Vector3.Zero, Vector3.UnitY * 10, Vector3.UnitZ * 10]
        );
        var rad = new Rad3d([poly], false, "nfmm/test");
        rad.Wheels = [new Rad3dWheelDef(
            new Vector3d(40, 20, 60),
            11,
            20,
            20,
            [poly, poly with { ColNum = null }]
        )];

        return rad;
    }
}
