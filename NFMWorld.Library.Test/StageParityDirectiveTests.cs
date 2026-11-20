using FixedMathSharp;
using NFMWorld.Sentry;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using TightPack;
using f64Euler = NFMWorldLibrary.FixedMath.f64Euler;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

/// <summary>
/// phy-addons stage directives: spawn overrides, ground pulse, flame palette, fset/fchk and
/// converging walls. Each case pins the format the original parser accepts.
/// </summary>
[TestClass]
public class StageParityDirectiveTests
{
    [ClassInitialize]
    public static void Init(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
                {
                    options.Dsn = "https://public@localhost/1";
                    options.TracesSampleRate = 0;
                }
            );
        }
        catch (InvalidOperationException)
        {
            // Another test class got there first; Sentry is a process-wide singleton.
        }

        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

        if (BackendGameSparker.stage_parts.Count != 0) return;
        var n = 0;
        foreach (var part in BackendGameSparker.StageRads)
            BackendGameSparker.stage_parts[n++] = RadParser.ParseRad(
                TheVFS.VFS.ReadAllText($"data/models/nfmm/stage/{part}.rad"),
                $"nfmm/{part}"
            );
    }

    private static StageLoader Load(params string[] lines) => StageLoader.FromLines("parity-test", lines);

    [TestMethod]
    public void StageLoader_ParsesStartOverrides()
    {
        var loader = Load(
            "xstart(2,100,200)",
            "zstart(2,-300,-400)",
            "ystart(2,260,270)",
            "xzstart(2,90,180)",
            "xystart(1,10)",
            "zystart(1,20)",
            "startheight(30)"
        );

        Assert.AreEqual(7, loader.Starts.Count);

        var x = loader.Starts[0];
        Assert.AreEqual(StartAxis.X, x.Axis);
        Assert.AreEqual(2, x.PlayerCount);
        CollectionAssert.AreEqual(new[] { 100f, 200f }, x.Values);

        Assert.AreEqual(StartAxis.Z, loader.Starts[1].Axis);
        Assert.AreEqual(StartAxis.Y, loader.Starts[2].Axis);
        Assert.AreEqual(StartAxis.Yaw, loader.Starts[3].Axis);
        Assert.AreEqual(StartAxis.Pitch, loader.Starts[4].Axis);
        Assert.AreEqual(StartAxis.Roll, loader.Starts[5].Axis);

        // startheight() is a wildcard for every player, 250 minus the parsed height.
        var height = loader.Starts[6];
        Assert.AreEqual(StartAxis.Y, height.Axis);
        Assert.AreEqual(0, height.PlayerCount);
        Assert.AreEqual(220f, height.Values[0]);
    }

    [TestMethod]
    public void StageLoader_AngleTagIsYaw()
    {
        var loader = Load("angle(2,45,90)");
        Assert.AreEqual(1, loader.Starts.Count);
        Assert.AreEqual(StartAxis.Yaw, loader.Starts[0].Axis);
        CollectionAssert.AreEqual(new[] { 45f, 90f }, loader.Starts[0].Values);
    }

    [TestMethod]
    public void BackendStage_StartTransform_AppliesOnlyTheMatchingCount()
    {
        var stage = new BackendStage(
            "parity-test",
            Load("xstart(2,100,200)", "zstart(2,-300,-400)", "xzstart(2,90,180)", "ystart(2,260,270)", "xstart(4,1,2,3,4)")
        );

        var first = stage.GetStartTransform(0, 2);
        Assert.AreEqual(100.0, (double)first.Position.X, 0.001);
        Assert.AreEqual(-300.0, (double)first.Position.Z, 0.001);
        Assert.AreEqual(260.0, (double)first.Position.Y, 0.001);
        Assert.AreEqual(90.0, (double)first.Rotation.Xz.Degrees, 0.001);

        var second = stage.GetStartTransform(1, 2);
        Assert.AreEqual(200.0, (double)second.Position.X, 0.001);
        Assert.AreEqual(180.0, (double)second.Rotation.Xz.Degrees, 0.001);

        // A four-player line does not describe a two-player race: the stock grid survives.
        var stock = stage.GetStartTransform(0, 3);
        Assert.AreEqual(0.0, (double)stock.Position.X, 0.001);
        Assert.AreEqual(-760.0, (double)stock.Position.Z, 0.001);
        Assert.AreEqual(0.0, (double)stock.Rotation.Xz.Degrees, 0.001);
    }

    [TestMethod]
    public void StageLoader_ParsesPulseAndFlamePalette()
    {
        var loader = Load(
            "pulsegrnd(0.95,0.04,0.845)",
            "flameColor1(1,2,3)",
            "flameColor2(4,5,6)",
            "flameColor3(7,8,9)",
            "flameSnapDiv(2,3,4)"
        );

        Assert.IsNotNull(loader.Pulse);
        Assert.AreEqual(0.95f, loader.Pulse.Value.A);
        Assert.AreEqual(0.04f, loader.Pulse.Value.B);
        Assert.AreEqual(0.845f, loader.Pulse.Value.C);

        Assert.IsNotNull(loader.FlameColors);
        var flames = loader.FlameColors.Value;
        Assert.AreEqual(new Color3(1, 2, 3), flames.Outer);
        Assert.AreEqual(new Color3(4, 5, 6), flames.Inner);
        Assert.AreEqual(new Color3(7, 8, 9), flames.Ember);
        Assert.AreEqual(2f, flames.SnapDivX);
        Assert.AreEqual(3f, flames.SnapDivY);
        Assert.AreEqual(4f, flames.SnapDivZ);
    }

    [TestMethod]
    public void BumpPieces_CarryTheBumpSurfaceType()
    {
        // Bump ramps are ordinary parts; what matters for parity is that their collision boxes
        // keep the model's `skid(3)`/`skid(4)` surface types the bump physics switches on.
        var loader = Load("set(nfmm/bumproad,0,0,0)", "set(nfmm/bsideoff,5000,0,0)");

        Assert.IsTrue(
            loader.pieces[0].Object.Boxes.Any(b => b.SurfaceType == SurfaceType.Bump),
            "OffroadBumpRamp must keep its bump surface"
        );
        Assert.IsTrue(
            loader.pieces[1].Object.Boxes.Any(b => b.SurfaceType == SurfaceType.BumpySides),
            "BumpySides must keep its surface"
        );
    }

    [TestMethod]
    public void StageLoader_FsetPutsTheAbsoluteHeightBeforeTheAngle()
    {
        var loader = Load("fset(nfmm/road,100,200,140,90)");

        var piece = loader.pieces[0];
        Assert.AreEqual(100.0, (double)piece.Position.X, 0.001);
        Assert.AreEqual(200.0, (double)piece.Position.Z, 0.001);
        Assert.AreEqual(140.0, (double)piece.Position.Y, 0.001);
        Assert.AreEqual(90.0, (double)piece.Rotation.Xz.Degrees, 0.001);
    }

    [TestMethod]
    public void StageLoader_FchkPutsTheAbsoluteHeightBeforeTheAngle()
    {
        var loader = Load("fchk(nfmm/checkpoint,100,200,140,90)");

        var piece = loader.pieces[0];
        Assert.AreEqual(PiecePlacementType.CheckPoint, piece.Type);
        Assert.AreEqual(140.0, (double)piece.Position.Y, 0.001);
        Assert.AreEqual(90.0, (double)piece.Rotation.Xz.Degrees, 0.001);
    }

    [TestMethod]
    public void StageLoader_PileIsProceduralAndDeterministic()
    {
        var loader = Load("ground(90,180,90)", "pile(7,4,3,500,-600)");

        var pile = loader.pieces[0];
        Assert.AreEqual(PiecePlacementType.Pile, pile.Type);
        Assert.AreEqual(7, pile.PileSeed);
        Assert.AreEqual(4, pile.PileWidth);
        Assert.AreEqual(3, pile.PileHeight);
        Assert.AreEqual(500.0, (double)pile.Position.X, 0.001);
        Assert.AreEqual(-600.0, (double)pile.Position.Z, 0.001);
        Assert.AreEqual(5, pile.Object.Polys.Length);
        Assert.AreEqual(5, pile.Object.Boxes.Length);

        var again = Load("ground(90,180,90)", "pile(7,4,3,500,-600)").pieces[0].Object;
        CollectionAssert.AreEqual(
            pile.Object.Polys.Select(p => p.Points[0]).ToArray(),
            again.Polys.Select(p => p.Points[0]).ToArray()
        );
    }

    [TestMethod]
    public void StageLoader_NewDirectives_SurviveARadpackRoundTrip()
    {
        var loader = Load(
            "xstart(2,100,200)",
            "pulsegrnd(0.95,0.04,0.845)",
            "flameColor1(1,2,3)",
            "set(28,10,20,0)",
            "pile(7,4,3,500,-600)",
            "maxl(1,-2000,0)",
            "maxr(1,2000,0)",
            "maxb(1,-2000,0)",
            "maxt(1,2000,0)"
        );

        var bytes = TightPackSerializer.Serialize(loader);
        var back = TightPackSerializer.Deserialize<StageLoader>(bytes);

        Assert.AreEqual(1, back.Starts.Count);
        Assert.AreEqual(StartAxis.X, back.Starts[0].Axis);
        CollectionAssert.AreEqual(new[] { 100f, 200f }, back.Starts[0].Values);
        Assert.AreEqual(0.95f, back.Pulse!.Value.A);
        Assert.AreEqual(new Color3(1, 2, 3), back.FlameColors!.Value.Outer);
        Assert.AreEqual(1, back.WallCentres.Count);
        Assert.AreEqual(10.0, (double)back.WallCentres[0].X, 0.001);

        var pile = back.pieces.Single(p => p.Type == PiecePlacementType.Pile);
        Assert.AreEqual(7, pile.PileSeed);
        Assert.AreEqual(4, pile.PileWidth);
        Assert.AreEqual(3, pile.PileHeight);
        Assert.AreEqual(5, pile.Object.Polys.Length);

        Assert.AreEqual(4, back.wallDefs.Count);
        Assert.IsTrue(back.wallDefs.Any(w => w.Direction == WallDirection.Right));
    }

    [TestMethod]
    public void StagePileBuilder_EverySeedAndSizeProducesACompletePile()
    {
        for (var seed = 0; seed < 48; seed++)
        {
            for (var width = 2; width <= 6; width++)
            {
                for (var height = 2; height <= 6; height++)
                {
                    var rad = StagePileBuilder.Build(seed, width, height, new Color3(100, 200, 100), new Color3(120, 180, 120));
                    Assert.AreEqual(5, rad.Polys.Length, $"seed {seed}, {width}x{height}");
                    Assert.AreEqual(5, rad.Boxes.Length, $"seed {seed}, {width}x{height}");
                    foreach (var poly in rad.Polys)
                        Assert.IsTrue(poly.Triangles.Length > 0, $"seed {seed}, {width}x{height}: empty triangulation");
                }
            }
        }
    }

    [TestMethod]
    public void ClosingWalls_ClosesTowardsTheCentreAndDropsOutOfBoundsCars()
    {
        var stage = new BackendStage(
            "parity-test",
            Load("maxl(3,-20000,0)", "maxr(3,20000,0)", "maxb(3,-20000,0)", "maxt(3,20000,0)")
        );

        Assert.IsTrue(stage.Walls.Enabled);
        stage.Walls.Begin(1, stage.StageLoader);
        stage.Walls.Update(0);

        Assert.IsFalse(stage.Walls.Closing);
        Assert.AreEqual(20000.0, (double)stage.Walls.Right, 0.001);

        stage.Walls.Update(3);
        Assert.IsTrue(stage.Walls.Closing);
        Assert.IsTrue((double)stage.Walls.Right < 20000.0);
        Assert.IsTrue((double)stage.Walls.Right > 3600.0);
        // The shrink is measured from the stage's original rectangle: a second update at the same
        // time must not compound it, or the walls slam shut within a couple of ticks.
        var afterFirstUpdate = (double)stage.Walls.Right;
        stage.Walls.Update(3);
        Assert.AreEqual(afterFirstUpdate, (double)stage.Walls.Right, 0.001);

        Assert.IsFalse(stage.Walls.IsInBounds((fix64)19000, (fix64)0));
        Assert.IsTrue(stage.Walls.IsInBounds((fix64)0, (fix64)0));

        // The moving boundary is collision geometry too, ahead of the static quadtree entries.
        Assert.IsTrue(stage.RetrievePointCollidables((fix64)19000, (fix64)0).Length > 0);
    }
}
