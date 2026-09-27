using NFMWorldLibrary.Gamemodes.DotNet;

namespace NFMWorld.Library.Test;

/// <summary>
/// Loads the compiled gamemode plugins the way the game does.
///
/// The failure this is most worth guarding is assembly identity: the plugins ship as
/// <c>gamemode.dll</c> in folders named for the gamemode, so an over-eager
/// <c>AssemblyName</c> would make every plugin the same assembly and the second one
/// loaded in a session would be rejected. Distinct identities are asserted below.
///
/// The tests run against the copies in this project's own output, which the gamemode
/// build target places there - so a wiring regression fails the suite rather than
/// surfacing as an unloaded gamemode in game.
/// </summary>
[TestClass]
public class GamemodePluginTests
{
    private static string OutputRoot => AppContext.BaseDirectory;

    private static GamemodePlugin? Load(string id)
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(OutputRoot);
        return GamemodePlugin.Load(id, $"data/gamemodes/{id}/gamemode.dll");
    }

    [TestMethod]
    public void Load_PvpPlugin_ReadsAssemblyAttributes()
    {
        var plugin = Load("nfmm/pvp");

        Assert.IsNotNull(plugin);
        Assert.AreEqual("nfmm/pvp", plugin.Id);
        Assert.AreEqual("PvP", plugin.Name);
        Assert.IsTrue(!string.IsNullOrWhiteSpace(plugin.Description));
        Assert.IsTrue(plugin.HasServerGamemode);
    }

    [TestMethod]
    public void Load_TimeTrialPlugin_ReadsAssemblyAttributes()
    {
        var plugin = Load("nfmm/timetrial");

        Assert.IsNotNull(plugin);
        Assert.AreEqual("nfmm/timetrial", plugin.Id);
        Assert.AreEqual("Time Trial", plugin.Name);
        Assert.IsTrue(plugin.HasServerGamemode);
    }

    /// <summary>
    /// Both plugins ship with the same file name, so they must not share an
    /// assembly identity - the runtime refuses to load a second assembly under one
    /// identity, which would make the second gamemode of a session unusable.
    /// </summary>
    [TestMethod]
    public void Load_BothPlugins_GetDistinctAssemblies()
    {
        var pvp = Load("nfmm/pvp");
        var timeTrial = Load("nfmm/timetrial");

        Assert.IsNotNull(pvp);
        Assert.IsNotNull(timeTrial);

        var pvpAssembly = pvp.GetClientGamemodeType().Assembly;
        var timeTrialAssembly = timeTrial.GetClientGamemodeType().Assembly;

        Assert.AreNotEqual(pvpAssembly, timeTrialAssembly);
        Assert.AreNotEqual(pvpAssembly.GetName().Name, timeTrialAssembly.GetName().Name);
    }

    [TestMethod]
    public void Load_MissingFile_ReturnsNull()
    {
        Assert.IsNull(Load("nfmm/nonexistent"));
    }

    [TestMethod]
    public void Load_PvpPlugin_ReadsConfigSchema()
    {
        var plugin = Load("nfmm/pvp");

        Assert.IsNotNull(plugin);

        var property = plugin.ConfigProperties.Single();
        Assert.AreEqual("constraint", property.Name);
        Assert.AreEqual(typeof(string), property.Type);
        Assert.AreEqual("Race constraint", property.Label);

        CollectionAssert.AreEqual(
            new[] { "racing", "wasting", "both" },
            property.Options.Select(o => o.Value).ToArray());
    }

    [TestMethod]
    public void Load_TimeTrialPlugin_HasNoConfigProperties()
    {
        var plugin = Load("nfmm/timetrial");

        Assert.IsNotNull(plugin);
        Assert.IsEmpty(plugin.ConfigProperties);
    }

    /// <summary>
    /// A gamemode that ships both a plugin and Luau scripts (every gamemode does
    /// right now) resolves to the plugin - that is the whole point of the port.
    /// </summary>
    [TestMethod]
    public void Resolver_PrefersPlugin_OverLuauScripts()
    {
        Load("nfmm/pvp");

        var factory = NFMWorldLibrary.Multiplayer.GamemodeFactory.Create(
            "nfmm/pvp", new Dictionary<string, object> { ["constraint"] = "both" });

        Assert.IsInstanceOfType<NativeGamemodeFactory>(factory);
    }

    [TestMethod]
    public void Resolver_CarriesConfigIntoTheGamemode()
    {
        Load("nfmm/pvp");

        var factory = (NativeGamemodeFactory)NFMWorldLibrary.Multiplayer.GamemodeFactory.Create(
            "nfmm/pvp", new Dictionary<string, object> { ["constraint"] = "both" });

        Assert.IsNotNull(factory);
        Assert.AreEqual("nfmm/pvp", factory.GamemodeId);
        Assert.IsTrue(factory.HasServerGamemode);
    }
}
