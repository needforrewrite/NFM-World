using NFMWorldLibrary;

namespace NFMWorld.Library.Test;

/// <summary>
/// The phy-addons physics directives are matched by prefix, so a stage's own scenery directives
/// (<c>lavavents()</c>, <c>damagevents()</c>) used to be swallowed by <c>lava</c>/<c>dam</c>: the
/// argument slice then started inside the directive name and the numeric parse threw, taking the
/// whole stage down. A user stage hit exactly that.
/// </summary>
[TestClass]
public class StageDirectivePrefixTests
{
    private static StageLoader Load(string body, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        var root = Path.Combine(Path.GetTempPath(), "NFMWorld-prefix", Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "data", "stages");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".txt"), body);
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(root);
        return new StageLoader(name);
    }

    [TestMethod]
    public void Lavavents_DoesNotPoisonTheLavaValue()
    {
        var physics = Load("lava(10)\nlavavents()\nname(after)").Physics;
        Assert.AreEqual(10f, physics.Lava);
    }

    [TestMethod]
    public void Damagevents_DoesNotPoisonTheDamageValue()
    {
        var physics = Load("dam(2)\ndamagevents()\nname(after)").Physics;
        Assert.AreEqual(2f, physics.Damage);
    }

    [TestMethod]
    public void CataclysmicCascadeStyleFurniture_StillLoads()
    {
        // The directives of the user stage that failed, minus the ones that only exist upstream.
        var loader = Load("""
            grndburn()
            firevfx(39,209,0)
            lavavents()
            ambience(6)
            lava(10)
            name(Cataclysmic Cascade)
            """);
        Assert.AreEqual("Cataclysmic Cascade", loader.Name);
        Assert.AreEqual(10f, loader.Physics.Lava);
    }

    [TestMethod]
    public void PhysicsDirectives_StillParseTheirArguments()
    {
        var physics = Load("""
            gravity(2.5)
            grip(1.5)
            powerloss(0.5)
            dam(2)
            lava(3)
            maxhit(2)
            recoil(1.5)
            rage(2)
            """).Physics;

        Assert.AreEqual(2.5f, physics.Gravity);
        Assert.AreEqual(1.5f, physics.Grip);
        Assert.AreEqual(0.5f, physics.Powerloss);
        Assert.AreEqual(2f, physics.Damage);
        Assert.AreEqual(3f, physics.Lava);
        Assert.AreEqual(600f, physics.MaxHit);
        Assert.AreEqual(1.5f, physics.Recoil);
        Assert.AreEqual(2f, physics.Rage);
    }
}
