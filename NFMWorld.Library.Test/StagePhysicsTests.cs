using NFMWorldLibrary;
using fix64 = FixedMathSharp.Fixed64;

namespace NFMWorld.Library.Test;

[TestClass]
public class StagePhysicsTests
{
    [TestMethod]
    public void StageLoader_ParsesPhysicsDirectives()
    {
        var root = Path.Combine(Path.GetTempPath(), "NFMWorld-stage-physics", Guid.NewGuid().ToString("N"));
        var stages = Path.Combine(root, "data", "stages");
        Directory.CreateDirectory(stages);
        File.WriteAllText(
            Path.Combine(stages, "props.txt"),
            "gravity(2.5)\ngrip(1.5)\npowerloss(0.5)\ndam(2)\nlava(3)\nmaxhit(2)\nrecoil(1.5)\nrage(2)\nstats()\nnoarrow()"
        );

        try
        {
            NFMWorldLibrary.TheVFS.VFS.MountDirectory(root);
            var physics = new StageLoader("props").Physics;

            Assert.AreEqual(2.5f, physics.Gravity);
            Assert.AreEqual(1.5f, physics.Grip);
            Assert.AreEqual(0.5f, physics.Powerloss);
            Assert.AreEqual(2f, physics.Damage);
            Assert.AreEqual(3f, physics.Lava);
            Assert.AreEqual(600f, physics.MaxHit); // multiplier on the stock 300
            Assert.AreEqual(1.5f, physics.Recoil);
            Assert.AreEqual(2f, physics.Rage);
            Assert.IsTrue(physics.RechargedStats);
            Assert.IsTrue(physics.NoArrow);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void StageLoader_NoDirectives_IsStock()
    {
        var root = Path.Combine(Path.GetTempPath(), "NFMWorld-stage-physics", Guid.NewGuid().ToString("N"));
        var stages = Path.Combine(root, "data", "stages");
        Directory.CreateDirectory(stages);
        File.WriteAllText(Path.Combine(stages, "stock.txt"), "snap(0,0,0)\nnlaps(3)");

        try
        {
            NFMWorldLibrary.TheVFS.VFS.MountDirectory(root);
            var physics = new StageLoader("stock").Physics;

            Assert.AreEqual(1f, physics.Gravity);
            Assert.AreEqual(1f, physics.Grip);
            Assert.AreEqual(1f, physics.Rage);
            Assert.AreEqual(300f, physics.MaxHit);
            Assert.IsFalse(physics.RechargedStats);
            Assert.IsFalse(physics.NoArrow);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void EffectiveStats_UndamagedAtStock_MatchesCarStats()
    {
        var stats = CarStats.Default;
        var effective = EffectiveStats.From(stats, new StagePhysics(), damagePoints: 0);

        Assert.AreEqual((double)stats.Moment, (double)effective.Moment, 1e-6);
        Assert.AreEqual((double)stats.Push, (double)effective.Push, 1e-6);
        Assert.AreEqual((double)stats.Revpush, (double)effective.Revpush, 1e-6);
        Assert.AreEqual((double)stats.Grip, (double)effective.Grip, 1e-6);
    }

    [TestMethod]
    public void EffectiveStats_DamagedWithRage_ScalesShoveBothWays()
    {
        var stats = CarStats.Default;
        var physics = new StagePhysics() with { Rage = 2f };
        var effective = EffectiveStats.From(stats, physics, damagePoints: stats.Maxmag);

        // phy-addons: damaged cars push harder and absorb less recoil.
        Assert.AreEqual((double)(stats.Push * 2), (double)effective.Push, 1e-4);
        Assert.AreEqual((double)(stats.Revpush / 2), (double)effective.Revpush, 1e-4);
    }

    [TestMethod]
    public void EffectiveStats_StageGrip_MultipliesGrip()
    {
        var stats = CarStats.Default;
        var effective = EffectiveStats.From(stats, new StagePhysics() with { Grip = 1.5f }, damagePoints: 0);

        Assert.AreEqual((double)(stats.Grip * (fix64)1.5f), (double)effective.Grip, 1e-4);
    }
}
