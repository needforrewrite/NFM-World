using FixedMathSharp;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Rendering;
using TightPack;

namespace NFMWorld.Library.Test;

[TestClass]
public class StageDestructionTests
{
    [TestMethod]
    [DataRow("", false, 0)]
    [DataRow("// maxmag(250)", false, 0)]
    [DataRow("maxmag(250)", true, 250)]
    [DataRow("maxmag(7)", true, 7)]
    [DataRow("maxmag(0)", true, 0)]
    [DataRow("maxmag(-10)", true, 0)]
    public void DurabilityRequiresAnExplicitPositiveMaxmag(string directive, bool specified, int durability)
    {
        var rad = RadParser.ParseRad(directive);
        var stage = CreateStage(rad);
        var obj = (StageObject)stage.Pieces[0];
        Assert.AreEqual(specified, rad.Stats.HasMaxmag);
        Assert.AreEqual(durability, obj.Durability);
    }

    [TestMethod]
    public void ModelSerializationPreservesOptInWithoutChangingTheCarDefault()
    {
        var explicitRad = RadParser.ParseRad("maxmag(250)");
        var implicitRad = RadParser.ParseRad("");
        Assert.AreEqual(7, implicitRad.Stats.Maxmag);
        var specified = TightPackSerializer.Deserialize<Rad3d>(TightPackSerializer.Serialize(explicitRad))!;
        var unspecified = TightPackSerializer.Deserialize<Rad3d>(TightPackSerializer.Serialize(implicitRad))!;
        Assert.IsTrue(specified.Stats.HasMaxmag);
        Assert.IsFalse(unspecified.Stats.HasMaxmag);
        Assert.AreEqual(250, ((StageObject)CreateStage(specified).Pieces[0]).Durability);
        Assert.AreEqual(0, ((StageObject)CreateStage(unspecified).Pieces[0]).Durability);
    }

    [TestMethod]
    public void MultipleCarsAccumulateDamageAndBreakRemovesEveryCollisionShapeOnce()
    {
        var stage = CreateStage(Model());
        var obj = (StageObject)stage.Pieces[0];
        var before = stage.RetrievePointCollidables(0, 0).Length;
        Assert.IsTrue(before > 0);
        var breaks = 0;
        var breakVelocity = default(Vector3d);
        stage.ObjectBroken += (_, velocity, _) => { breaks++; breakVelocity = velocity; };
        var impact = Hit(0, 100);
        stage.DamageObject(impact);
        Assert.AreEqual(160, obj.Damage);
        Assert.IsFalse(obj.IsDestroyed);
        stage.DamageObject(impact);
        Assert.AreEqual(250, obj.Damage);
        Assert.IsTrue(obj.IsDestroyed);
        Assert.AreEqual(0, stage.Pieces.Count);
        Assert.AreEqual(0, stage.RetrievePointCollidables(0, 0).Length);
        Assert.AreEqual(impact.Velocity, breakVelocity);
        Assert.IsNull(stage.DamageObject(impact));
        Assert.AreEqual(1, breaks);
    }

    [TestMethod]
    public void SnapshotsCatchUpDamageAndBreaksWithoutReplayingShards()
    {
        var source = CreateStage(Model(), Model());
        var target = CreateStage(Model(), Model());
        source.DamageObject(Hit(0, 100));
        source.DamageObject(Hit(1, 200));
        var effects = 0;
        var damageEffects = 0;
        target.ObjectBroken += (_, _, shatter) => { if (shatter) effects++; };
        target.ObjectDamaged += (_, playSound) => { if (playSound) damageEffects++; };

        foreach (var state in source.CaptureDestruction())
        {
            target.ApplyPieceState(state, shatter: false);
            target.ApplyPieceState(state, shatter: false);
        }

        Assert.AreEqual(160, target.GetDestructible(0)!.Damage);
        Assert.IsTrue(target.GetDestructible(1)!.IsDestroyed);
        Assert.AreEqual(1, target.Pieces.Count);
        Assert.AreEqual(0, effects);
        Assert.AreEqual(0, damageEffects);
        Assert.AreEqual(source.GetDestructible(0)!.Deformation, target.GetDestructible(0)!.Deformation);
        target.ApplyPieceState(new StagePieceState { PieceId = 0, Damage = 1 });
        Assert.AreEqual(160, target.GetDestructible(0)!.Damage);
        Assert.AreEqual(source.GetDestructible(0)!.Deformation, target.GetDestructible(0)!.Deformation);
    }

    [TestMethod]
    public void ResetRestoresOriginalOrderDurabilityAndCollision()
    {
        var stage = CreateStage(Model(), new Rad3d([], false, "solid"), Model());
        var original = stage.Pieces.ToArray();
        stage.DamageObject(Hit(2, 200));
        stage.DamageObject(Hit(0, 200));
        stage.ResetDestruction();
        CollectionAssert.AreEqual(original, stage.Pieces.ToArray());
        Assert.AreEqual(0, stage.GetDestructible(0)!.Damage);
        Assert.AreEqual(0, stage.GetDestructible(2)!.Damage);
        Assert.AreEqual(default(Vector3d), stage.GetDestructible(0)!.Deformation);
        Assert.AreEqual(default(Vector3d), stage.GetDestructible(2)!.Deformation);
        Assert.AreEqual(0, stage.CaptureDestruction().Count());
        Assert.IsTrue(stage.RetrievePointCollidables(0, 0).Length > 0);
        Assert.IsNotNull(stage.DamageObject(Hit(2, 200)));
    }

    [TestMethod]
    public void NetworkImpactWaitsForAuthorityAndIgnoresScrapesAndUnknownIds()
    {
        var stage = CreateStage(Model());
        StageImpact received = default;
        var owner = -1;
        stage.ObjectHit += (car, impact) => { owner = car; received = impact; };
        stage.ReportObjectHit(3, Hit(0, 200));
        Assert.AreEqual(3, owner);
        Assert.AreEqual(0, stage.GetDestructible(0)!.Damage);
        Assert.IsNull(stage.DamageObject(Hit(0, 20)));
        Assert.IsNull(stage.DamageObject(Hit(999, 200)));
        Assert.IsNotNull(stage.DamageObject(received));
        Assert.IsTrue(stage.GetDestructible(0)!.IsDestroyed);
    }

    [TestMethod]
    public void CheckpointsAndFixHoopsStayIntactEvenWithMaxmag()
    {
        foreach (var type in new[] { PiecePlacementType.CheckPoint, PiecePlacementType.FixHoop })
        {
            var loader = new StageLoader();
            loader.pieces.Add(new PiecePlacement(type, Model(), default, default));
            var stage = new BackendStage("destruction-test", loader, throwOnError: true);
            Assert.AreEqual(0, ((StageObject)stage.Pieces[0]).Durability);
            Assert.IsNull(stage.DamageObject(Hit(0, 200)));
        }
    }

    [TestMethod]
    public void SurvivingHitsDeformOnlyTheirOwnInstanceAndSnapshotsRestoreTheSameShape()
    {
        var model = Model();
        model.Polys =
        [
            new Rad3dPoly(
                new NFMWorldLibrary.Util.Color3(220, 180, 100),
                null,
                PolyType.Flat,
                null,
                0,
                [new(-100, -100, 0), new(100, -100, 0), new(100, 100, 0), new(-100, 100, 0)],
                [0, 1, 2, 0, 2, 3]
            )
        ];
        var originalPoints = model.Polys[0].Points.ToArray();
        var stage = CreateStage(model, model);
        var hit = stage.GetDestructible(0)!;
        var untouched = stage.GetDestructible(1)!;
        stage.DamageObject(Hit(0, 60));
        var firstDent = StageObjectDamage.Deform(hit);
        Assert.IsFalse(firstDent[0].Points.SequenceEqual(originalPoints));
        CollectionAssert.AreEqual(originalPoints, model.Polys[0].Points);
        Assert.AreSame(model.Polys, StageObjectDamage.Deform(untouched));

        stage.DamageObject(Hit(0, 60));
        var secondDent = StageObjectDamage.Deform(hit);
        Assert.IsFalse(hit.IsDestroyed);
        Assert.IsFalse(firstDent[0].Points.SequenceEqual(secondDent[0].Points));
        Assert.AreEqual(model.Polys[0].Color, firstDent[0].Color);
        Assert.AreEqual(model.Polys[0].Color, secondDent[0].Color);

        var reconnected = CreateStage(model, model);
        foreach (var state in stage.CaptureDestruction())
            reconnected.ApplyPieceState(state, shatter: false);
        var restored = StageObjectDamage.Deform(reconnected.GetDestructible(0)!);
        CollectionAssert.AreEqual(secondDent[0].Points, restored[0].Points);
        Assert.AreEqual(secondDent[0].Color, restored[0].Color);
        CollectionAssert.AreEqual(originalPoints, model.Polys[0].Points);
        stage.ResetDestruction();
        Assert.AreSame(model.Polys, StageObjectDamage.Deform(hit));
    }

    [TestMethod]
    public void DamageFeedbackFiresOnlyForNewSurvivingHits()
    {
        var stage = CreateStage(Model());
        var damageSounds = 0;
        var breaks = 0;
        stage.ObjectDamaged += (_, playSound) => { if (playSound) damageSounds++; };
        stage.ObjectBroken += (_, _, _) => breaks++;
        var first = stage.DamageObject(Hit(0, 60))!.Value;
        Assert.AreEqual(1, damageSounds);
        stage.ApplyPieceState(first);
        stage.ApplyPieceState(new StagePieceState { PieceId = 0, Damage = 1 });
        Assert.AreEqual(1, damageSounds);
        stage.DamageObject(Hit(0, 200));
        Assert.AreEqual(1, damageSounds);
        Assert.AreEqual(1, breaks);
    }

    private static Rad3d Model() => new(
        [],
        new CarStats { Maxmag = 250, HasMaxmag = true },
        [],
        null,
        [new Rad3dBoxDef(0, 90, new Vector3d(100, 100, 100), default, SurfaceType.Road, 1, false, default)],
        [],
        false,
        []
    );

    private static BackendStage CreateStage(params Rad3d[] models)
    {
        var loader = new StageLoader();
        foreach (var model in models)
            loader.pieces.Add(new PiecePlacement(PiecePlacementType.CollisionObject, model, default, default));

        return new BackendStage("destruction-test", loader, throwOnError: true);
    }

    private static StageImpact Hit(int piece, int speed) => new()
    {
        PieceId = piece, Impulse = new Vector3d(0, 0, speed), Velocity = new Vector3d(speed / 2, -10, speed)
    };
}
