using System.Runtime.CompilerServices;
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

    /// <summary>
    /// The runtime check hosts call each frame: it must not fire on the frame that released
    /// the plugin, whose stack still roots it, and it must stay quiet on a clean teardown.
    /// </summary>
    [TestMethod]
    public void Verify_StaysQuietOnACleanRelease()
    {
        PluginUnloadVerifier.Reset();
        RunRaceAndTearItDown();

        // Same frame as the release: the stack still roots the plugin, so the verifier
        // has to withhold judgement rather than report a leak it cannot actually see.
        PluginUnloadVerifier.Verify();

        // A later frame, where the teardown's frames are gone: this is the real check.
        VerifyOnAFreshFrame();
    }

    /// <summary>
    /// ...and it must actually catch a leak, or it would be a check that can never fail.
    /// A plugin pinned by a type of its own resolves against the plugin's context, so the
    /// assembly cannot be collected no matter what the teardown drops.
    /// </summary>
    [TestMethod]
    public void Verify_ReportsALeak()
    {
        PluginUnloadVerifier.Reset();
        MountOutput();

        var plugin = GamemodePlugin.Load("nfmm/pvp", "data/gamemodes/nfmm/pvp/gamemode.dll")!;

        var leaked = plugin.Assembly.GetExportedTypes().First(t => t.Name == "CountdownGoEvent");
        PluginUnloadVerifier.Arm("nfmm/pvp", new WeakReference(plugin.Assembly));
        plugin.Unload();

        PluginUnloadVerifier.Verify(); // same frame: withholds, the stack still roots it

        Assert.ThrowsExactly<InvalidOperationException>(VerifyOnAFreshFrame);

        GC.KeepAlive(leaked);
    }

    /// <summary>
    /// The <c>map</c> dev command starts a second race on another stage while the first is
    /// still live, and the replaced phase's disposal - at end-of-frame, once the new race is
    /// already running - is what releases the outgoing run's factory. That is only safe
    /// because each run brings its own: a factory releases the plugin it owns in
    /// <see cref="BaseGamemodeFactory.End"/>, so two runs sharing one would have the outgoing
    /// race unload the plugin out from under the incoming one. That is the crash the verifier
    /// reported from the game, and this asserts the arrangement that replaces it.
    ///
    /// Teardown happens in <see cref="StartAndReplace()`"/> so that this method's own frame -
    /// which would otherwise still root the outgoing assembly - is not the one being checked.
    /// </summary>
    [TestMethod]
    public void AReplacementRun_LeavesTheOutgoingRunsPluginAlone()
    {
        PluginUnloadVerifier.Reset();

        var (outgoingAssembly, incoming, incomingHost) = StartAndReplace();

        PluginUnloadVerifier.Verify(); // same frame: withholds, the stack still roots it
        VerifyOnAFreshFrame();         // clean release, so no leak is reported

        ForceCollect();

        Assert.IsFalse(outgoingAssembly.IsAlive,
            "the replaced race's plugin should still unload even though a second race of the same gamemode is running");

        // Still usable, because it was never the one released.
        Assert.IsTrue(((NativeGamemodeFactory)incoming).Plugin.IsAlive);
        incomingHost.Update();

        incomingHost.Dispose();
        incoming.End();
    }

    private static (WeakReference OutgoingAssembly, BaseGamemodeFactory Incoming, LocalRaceHost IncomingHost)
        StartAndReplace()
    {
        MountOutput();

        var parameters = new ClientGamemodeParameters
        {
            Players = [new ClientSidePlayerInfo("test", "nfmm/radicalone", new Color3(255, 0, 0),
                isBot: false, isClientPlayer: true)]
        };

        var outgoing = GamemodeFactory.Create("nfmm/pvp", new Dictionary<string, object> { ["constraint"] = "both" });
        var outgoingHost = LocalRaceHost.Create("empty", outgoing, parameters);
        var outgoingAssembly = new WeakReference(((NativeGamemodeFactory)outgoing).Plugin.Assembly);

        // What `map` builds now: an equivalent factory of its own, config carried over.
        var incoming = GamemodeFactory.Create("nfmm/pvp", outgoing.Config!);
        var incomingHost = LocalRaceHost.Create("empty", incoming, parameters);

        // The replaced race's disposal, which the phase manager defers to end-of-frame.
        outgoingHost.Dispose();
        outgoing.End();

        return (outgoingAssembly, incoming, incomingHost);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void VerifyOnAFreshFrame() => PluginUnloadVerifier.Verify();
}
