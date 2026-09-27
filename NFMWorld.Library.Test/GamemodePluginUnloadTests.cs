using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Library.Test;

/// <summary>
/// Proves a gamemode plugin's assembly is actually released when its race is over,
/// rather than staying resident for the process lifetime.
///
/// Worth a test of its own because the failure is invisible: calling
/// <c>AssemblyLoadContext.Unload()</c> on a context something still references does
/// nothing, quietly, with no error anywhere. The reference that pins it in practice
/// is MemoryPack - a plugin's generated formatters register themselves into
/// MemoryPack's static table from the plugin's module initializer, holding the
/// plugin's types from there. <see cref="End_ReleasesTheAssembly_AfterItsTypesWereSerialized"/>
/// covers that; without the cleanup in <see cref="GamemodePlugin.Unload"/> it fails.
/// </summary>
[TestClass]
public class GamemodePluginUnloadTests
{
    private static void MountOutput() => NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

    /// <summary>
    /// Drives a whole race through the same factory and host the game uses, then tears it
    /// down. Returns a weak handle to the plugin's assembly, which stays alive exactly as
    /// long as its load context does, plus the host and factory - deliberately still
    /// reachable, because the game holds on to those (a phase kept alive by an event
    /// subscription outlives its own disposal) and the teardown has to release the plugin
    /// anyway. Keeping them is what makes this a test of the teardown rather than of the
    /// garbage collector.
    /// </summary>
    private static (WeakReference Assembly, object[] Retained) RunRaceAndTearItDown(
        Action<GamemodePlugin>? whileLoaded = null)
    {
        MountOutput();

        var factory = GamemodeFactory.Create("nfmm/pvp");
        var plugin = ((NativeGamemodeFactory)factory).Plugin;

        // "empty" is the one stage in the test output that needs no car or geometry assets.
        var host = LocalRaceHost.Create("empty", factory, new ClientGamemodeParameters
        {
            Players = [new ClientSidePlayerInfo("test", "nfmm/radicalone", new Color3(255, 0, 0),
                isBot: false, isClientPlayer: true)]
        });

        var assembly = new WeakReference(plugin.Assembly);

        whileLoaded?.Invoke(plugin);

        host.Dispose();
        factory.End();

        return (assembly, [host, factory]);
    }

    private static void ForceCollect()
    {
        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [TestMethod]
    public void End_ReleasesTheAssembly()
    {
        var (assembly, retained) = RunRaceAndTearItDown();

        ForceCollect();

        Assert.IsFalse(assembly.IsAlive,
            "the plugin's assembly should be collectible once the race is over and the factory ended");

        GC.KeepAlive(retained);
    }

    /// <summary>
    /// The case that would silently fail without the formatter cleanup: a gamemode that
    /// has actually used its own MemoryPack event types. Serializing registers the
    /// plugin's formatter into MemoryPack's static table, which holds the plugin's type -
    /// and through it the whole load context - for the process lifetime.
    /// </summary>
    [TestMethod]
    public void End_ReleasesTheAssembly_AfterItsTypesWereSerialized()
    {
        var (assembly, retained) = RunRaceAndTearItDown(plugin =>
        {
            var eventType = plugin.Assembly.GetExportedTypes()
                .First(t => t.Name == "CountdownGoEvent");

            // Mirrors how the gamemode sends an event: the union base is what goes on
            // the wire, and serializing it is what registers the plugin's formatters.
            MemoryPack.MemoryPackSerializer.Serialize(eventType.BaseType!, Activator.CreateInstance(eventType));
        });

        ForceCollect();

        Assert.IsFalse(assembly.IsAlive,
            "the plugin's assembly was still resident after the race, so MemoryPack is still holding its types");

        GC.KeepAlive(retained);
    }

    [TestMethod]
    public void End_IsIdempotent()
    {
        MountOutput();

        var factory = GamemodeFactory.Create("nfmm/pvp");

        factory.End();
        factory.End();

        Assert.AreEqual("nfmm/pvp", factory.GamemodeId);
    }
}
