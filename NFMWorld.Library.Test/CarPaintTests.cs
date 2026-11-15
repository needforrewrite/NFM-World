using System.Numerics;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Paint copies keep cached models and other players' colours unchanged.
/// </summary>
[TestClass]
[DoNotParallelize]
public class CarPaintTests
{
    private const string CarId = "nfmm/paint-test";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "NFMWorld-paint-tests", Guid.NewGuid().ToString("N"));

    private VirtualFS _fs = null!;

    [TestInitialize]
    public void Initialize()
    {
        _fs = new VirtualFS();
        _fs.MountWriteDestination(_root);
        CarPaint.Load(_fs);
    }

    [TestCleanup]
    public void Cleanup()
    {
        CarPaint.Load(new VirtualFS());
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void Apply_RepaintsOnlyTheChosenSlot()
    {
        var first = new Color3(41, 41, 41);
        var second = new Color3(215, 215, 215);
        var untagged = new Color3(10, 20, 30);
        var rad = Car(first, second, untagged);

        CarPaint.Set(CarId, 0, new Color3(9, 8, 7), _fs);
        CarPaint.Apply(rad, CarId);

        Assert.AreEqual(new Color3(15, 15, 15), rad.Polys[0].Color);
        Assert.AreEqual(second, rad.Polys[1].Color);
        Assert.AreEqual(untagged, rad.Polys[2].Color);
    }

    [TestMethod]
    public void Apply_ChangingAnOverride_RepaintsFromTheOriginal()
    {
        var first = new Color3(41, 41, 41);
        var rad = Car(first, new Color3(215, 215, 215), new Color3(10, 20, 30));

        CarPaint.Set(CarId, 0, new Color3(9, 8, 7), _fs);
        CarPaint.Apply(rad, CarId);
        CarPaint.Set(CarId, 0, new Color3(30, 40, 50), _fs);
        CarPaint.Apply(rad, CarId);

        Assert.AreEqual(new Color3(30, 40, 50), rad.Polys[0].Color);
    }

    [TestMethod]
    public void Reset_RestoresTheOriginalColor()
    {
        var first = new Color3(41, 41, 41);
        var rad = Car(first, new Color3(215, 215, 215), new Color3(10, 20, 30));

        CarPaint.Set(CarId, 0, new Color3(9, 8, 7), _fs);
        CarPaint.Apply(rad, CarId);
        CarPaint.Reset(CarId, _fs);
        CarPaint.Apply(rad, CarId);

        Assert.AreEqual(first, rad.Polys[0].Color);
    }

    [TestMethod]
    public void Overrides_SurviveAReload()
    {
        var first = new Color3(41, 41, 41);
        CarPaint.Set(CarId, 0, new Color3(9, 8, 7), _fs);
        CarPaint.Save(_fs);

        var saved = new VirtualFS();
        saved.MountDirectory(_root);

        CarPaint.Load(new VirtualFS());
        var afterClear = Car(first, new Color3(215, 215, 215), new Color3(10, 20, 30));
        CarPaint.Apply(afterClear, CarId);
        Assert.AreEqual(first, afterClear.Polys[0].Color);

        CarPaint.Load(saved);
        var reloaded = Car(first, new Color3(215, 215, 215), new Color3(10, 20, 30));
        CarPaint.Apply(reloaded, CarId);
        Assert.AreEqual(new Color3(15, 15, 15), reloaded.Polys[0].Color);
    }

    [TestMethod]
    public void Load_ClampsOlderDarkOverrides()
    {
        using (var stream = _fs.OpenWrite(CarPaint.SavePath))
        using (var writer = new StreamWriter(stream))
            writer.Write("{\"version\":1,\"cars\":{\"nfmm/paint-test\":{\"0\":65280}}}");

        var saved = new VirtualFS();
        saved.MountDirectory(_root);
        CarPaint.Load(saved);
        var rad = Car(new Color3(41, 41, 41), new Color3(215, 215, 215), new Color3(0, 0, 0));
        CarPaint.Apply(rad, CarId);

        Assert.AreEqual(new Color3(15, 255, 15), rad.Polys[0].Color);
        Assert.AreEqual(new Color3(0, 0, 0), rad.Polys[2].Color);
    }

    [TestMethod]
    public void Slots_ListsOnlyTheDeclaredSlots()
    {
        var rad = Car(new Color3(41, 41, 41), new Color3(215, 215, 215), new Color3(10, 20, 30));

        var slots = CarPaint.Slots(rad);

        Assert.AreEqual(2, slots.Count);
        Assert.AreEqual(0, slots[0].Slot);
        Assert.AreEqual(1, slots[1].Slot);
    }

    [TestMethod]
    public void PlayersUsingSameModelKeepIndependentPaintAndBotsUseStockColors()
    {
        var original = new Color3(40, 80, 120);
        var chosen = new Color3(210, 50, 60);
        var remoteColor = new Color3(50, 210, 80);
        var source = Car(original, new Color3(100, 100, 100), new Color3(0, 0, 0));
        source.Wheels = new Rad3dWheelDef[4];
        CarPaint.Set(CarId, 0, chosen, _fs);
        var hadPrevious = BackendGameSparker.dynamic_models.TryGetValue(CarId, out var previous);
        BackendGameSparker.dynamic_models[CarId] = (0, source);
        try
        {
            var playerInfo = new ClientSidePlayerInfo("local", CarId, default, false, true)
            {
                CarColors = CarPaint.SavedColors(CarId)
            };
            var remoteInfo = new ClientSidePlayerInfo("remote", CarId, default, false, false)
            {
                CarColors = [remoteColor, null, null, null]
            };
            var player = new BackendCar(playerInfo, 0, 0, 0);
            var bot = new BackendCar(new ClientSidePlayerInfo("bot", CarId, default, true, false), 1, 0, 0);
            var remote = new BackendCar(remoteInfo, 2, 0, 0);

            Assert.AreEqual(chosen, player.Rad.Polys[0].Color);
            Assert.AreEqual(original, bot.Rad.Polys[0].Color);
            Assert.AreEqual(remoteColor, remote.Rad.Polys[0].Color);
            Assert.AreEqual(original, BackendGameSparker.GetCar(CarId).Rad!.Polys[0].Color);
            Assert.AreNotSame(source.Polys, player.Rad.Polys);
            Assert.AreNotSame(player.Rad.Polys, remote.Rad.Polys);

            CarPaint.Set(CarId, 0, new Color3(255, 255, 255), _fs);
            Assert.AreEqual(chosen, player.Rad.Polys[0].Color);
            Assert.AreEqual(remoteColor, remote.Rad.Polys[0].Color);
        }
        finally
        {
            if (hadPrevious) BackendGameSparker.dynamic_models[CarId] = previous;
            else BackendGameSparker.dynamic_models.Remove(CarId);
        }
    }

    [TestMethod]
    public void PreviewPaintAndResetLeaveTheCachedModelUnchanged()
    {
        var original = new Color3(40, 80, 120);
        var source = Car(original, new Color3(100, 100, 100), new Color3(0, 0, 0));
        var preview = CarPaint.CreateInstance(source);
        CarPaint.Set(CarId, 0, new Color3(210, 50, 60), _fs);
        CarPaint.Apply(preview, CarId);
        Assert.AreEqual(original, source.Polys[0].Color);
        Assert.AreEqual(original, CarPaint.CreateInstance(preview).Polys[0].Color);
        CarPaint.Reset(CarId, _fs);
        CarPaint.Apply(preview, CarId);
        Assert.AreEqual(original, preview.Polys[0].Color);
    }

    [TestMethod]
    public void NetworkPaintIsLimitedToFourSlotsAndClamped()
    {
        var colors = CarPaint.NormalizeColors([new Color3(0, 300, -5), null, null, null, new Color3(255, 0, 0)]);
        Assert.AreEqual(4, colors.Length);
        Assert.AreEqual(new Color3(15, 255, 15), colors[0]!.Value);
        Assert.IsNull(colors[1]);
    }

    private static Rad3d Car(Color3 first, Color3 second, Color3 untagged)
        => new(
            [first, second],
            CarStats.Default,
            [],
            null,
            [],
            [Poly(first, 0), Poly(second, 1), Poly(untagged, null)],
            false,
            [],
            CarId
        );

    private static Rad3dPoly Poly(Color3 color, int? slot)
        => new(
            color,
            slot,
            PolyType.Flat,
            null,
            0,
            [new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 10, 0)]
        );
}
