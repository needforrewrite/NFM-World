using System.Reflection;
using System.Security;
using NFMWorldLibrary.Gamemodes.DotNet;

namespace NFMWorld.Library.Test;

/// <summary>
/// Proves the CasCore policy actually stops a plugin from reaching outside it.
///
/// The hostile plugin - <see cref="NFMWorld.Gamemode.Hostile"/> - contains one probe per
/// thing a downloaded gamemode must not be able to do. Every probe has to fail, or the
/// sandbox is decoration.
///
/// The MemoryPack case is the subtle one: a plugin's generated formatters call
/// <c>Unsafe.SizeOf</c>/<c>Unsafe.As</c> and MemoryPack's own read/write helpers, which
/// are memory-unsafe in isolation. They are allowed only through MemoryPack's fixed
/// signatures, whose reads and writes are bounds-checked by <c>GetSpanReference</c>, so a
/// plugin keeps its serializer without gaining arbitrary memory access.
/// </summary>
[TestClass]
public class PluginSandboxTests
{
    private static Assembly _hostile = null!;
    private static GamemodePlugin? _plugin;

    [ClassInitialize]
    public static void LoadPlugin(TestContext _)
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);

        _plugin = GamemodePlugin.Load("sandbox/hostile", "data/gamemodes/sandbox/hostile/gamemode.dll");
        Assert.IsNotNull(_plugin, "the hostile plugin has to load, or the sandbox is never exercised");
        _hostile = _plugin.Assembly;
    }

    private static void AssertBlocked(string method, params object?[] args)
    {
        var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod(method, BindingFlags.Public | BindingFlags.Static)!;

        var exception = Assert.ThrowsExactly<TargetInvocationException>(() => target.Invoke(null, args));

        Assert.IsInstanceOfType(exception.InnerException, typeof(SecurityException),
            $"{method} reached something the policy should have blocked");
    }

    [TestMethod]
    public void ReadingAFile_IsBlocked() => AssertBlocked("ReadAFile");

    [TestMethod]
    public void WritingAFile_IsBlocked() => AssertBlocked("WriteAFile");

    [TestMethod]
    public void OpeningASocket_IsBlocked() => AssertBlocked("OpenASocket");

    [TestMethod]
    public void CallingAHiddenHostMember_IsBlocked() => AssertBlocked("CallAHiddenMember");

    [TestMethod]
    public void LoadingANativeLibrary_IsBlocked() => AssertBlocked("LoadNative");

    [TestMethod]
    public void UninitializedMemory_IsBlocked() => AssertBlocked("UninitializedMemory");

    [TestMethod]
    public void ReinterpretingMemory_IsBlocked() => AssertBlocked("Reinterpret", 0);

    /// <summary>
    /// The counterpart: the plugin's own serializer has to keep working, or the sandbox
    /// would break every gamemode that sends events. Its generated formatter reaches
    /// MemoryPack's unsafe primitives, which are on the policy by signature only.
    /// </summary>
    [TestMethod]
    public void SerializingAPluginEvent_Works()
    {
        var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod("SerializeAnEvent", BindingFlags.Public | BindingFlags.Static)!;

        var payload = (byte[])target.Invoke(null, null)!;

        Assert.IsTrue(payload.Length > 0);
    }

}
