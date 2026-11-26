using System.Reflection;
using System.Security;
using TightPack;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary;

namespace NFMWorld.Library.Test;

/// <summary>
/// Proves the CasCore policy actually stops a plugin from reaching outside it.
///
/// The hostile plugin - <see cref="NFMWorld.Gamemode.Hostile"/> - contains one probe per
/// thing a downloaded gamemode must not be able to do. Every probe has to fail, or the
/// sandbox is decoration.
///
/// The serialization case is the subtle one: a plugin's generated code calls TightPack's
/// read/write helpers, which are memory-unsafe in isolation. They are allowed only through
/// TightPack's fixed signatures, whose reads and writes are bounds-checked by <c>GetSpan</c>,
/// so a plugin keeps its serializer without gaining arbitrary memory access.
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

        Assert.IsInstanceOfType(
            exception.InnerException,
            typeof(SecurityException),
            $"{method} reached something the policy should have blocked"
        );
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

    /// <summary>
    /// A type marked [PluginVisible] with no visibility argument is visible through its
    /// marked members only. Reading one of its private fields must be refused, or the
    /// marker is a grant of internals nobody asked for.
    /// </summary>
    [TestMethod]
    public void ReadingAPrivateFieldOfAMarkedType_IsBlocked()
    {
        // CasCore's IL verifier catches the direct ldfld first, so this is an invalid
        // program rather than a policy violation. Either way the reach does not run.
        var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod("ReadAPrivateField", BindingFlags.Public | BindingFlags.Static)!;

        Assert.ThrowsExactly<TargetInvocationException>(() => target.Invoke(null, null));
    }

    [TestMethod]
    public void UninitializedMemory_IsBlocked() => AssertBlocked("UninitializedMemory");

    [TestMethod]
    public void ReinterpretingMemory_IsBlocked() => AssertBlocked("Reinterpret", 0);

    /// <summary>
    /// A subscription stores the plugin's delegate in a host object that outlives the gamemode,
    /// so nothing on the plugin side can release it. Denied by the event's own policy entry,
    /// whether or not its declaration was ever marked.
    /// </summary>
    [TestMethod]
    public void SubscribingToAPhysicsStep_IsBlocked()
        => AssertBlocked("SubscribeToAPhysicsStep", new CarPhysics(CarStats.Default, 0, false));

    /// <summary>
    /// The same reach on a type exposed with all of its public members: the accessor would be
    /// admitted by the type's marker alone, which is why events are denied as a class.
    /// </summary>
    [TestMethod]
    public void SubscribingToAHostCollection_IsBlocked()
        => AssertBlocked("SubscribeToAHostCollection", new NFMWorldLibrary.Util.ObservableUnlimitedArray<int>());

    [TestMethod]
    public void StartingAThread_IsBlocked() => AssertBlocked("StartAThread");

    [TestMethod]
    public void StartingATimer_IsBlocked() => AssertBlocked("StartATimer");

    [TestMethod]
    public void StartingAnElapsedTimer_IsBlocked() => AssertBlocked("StartAnElapsedTimer");

    [TestMethod]
    public void QueueingWorkOnThePool_IsBlocked() => AssertBlocked("QueueWorkOnThePool");

    [TestMethod]
    public void SchedulingATask_IsBlocked() => AssertBlocked("ScheduleATask");

    [TestMethod]
    public void StartingAFactoryTask_IsBlocked() => AssertBlocked("StartAFactoryTask");

    [TestMethod]
    public void HandlingCancelKeyPress_IsBlocked() => AssertBlocked("HandleCancelKeyPress");

    [TestMethod]
    public void WatchingUnobservedTaskExceptions_IsBlocked() => AssertBlocked("WatchUnobservedTaskExceptions");

    [TestMethod]
    public void ListeningToDiagnostics_IsBlocked() => AssertBlocked("ListenToDiagnostics");

    [TestMethod]
    public void InstallingASynchronizationContext_IsBlocked() => AssertBlocked("InstallASynchronizationContext");

    /// <summary>
    /// The other half of the provider's exposure: a plugin cannot declare a formatter of its own,
    /// so it cannot put an entry of its own type into the host's static table under a host key.
    /// The base formatter's constructor is what refuses it.
    /// </summary>
    [TestMethod]
    public void DeclaringAFormatter_IsBlocked() => AssertBlocked("DeclareAFormatterForAHostType");

    /// <summary>
    /// The counterpart: the plugin's own serializer has to keep working, or the sandbox
    /// would break every gamemode that sends events. Its generated code reaches TightPack's
    /// primitives, which are on the policy by signature only.
    /// </summary>
    [TestMethod]
    public void SerializingAPluginEvent_Works()
    {
        var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod("SerializeAnEvent", BindingFlags.Public | BindingFlags.Static)!;

        var payload = (byte[])target.Invoke(null, null)!;

        Assert.IsTrue(payload.Length > 0);
    }

    [TestMethod]
    public void SerializingAnEventWithProviderResolvedMembers_Works()
    {
        var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod("SerializeACollectionEvent", BindingFlags.Public | BindingFlags.Static)!;

        var payload = (byte[])target.Invoke(null, null)!;

        Assert.IsTrue(payload.Length > 0);

        // Deserialising constructs the array and writes its elements, and reaching the serializer
        // from a byte[] needs the ReadOnlySpan conversion; neither is exercised by serialising.
        var roundTrip = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod("RoundTripACollectionEvent", BindingFlags.Public | BindingFlags.Static)!;

        var grid = (int[,])roundTrip.Invoke(null, null)!;

        Assert.AreEqual(2, grid.GetLength(0));
        Assert.AreEqual(3, grid.GetLength(1));
        Assert.AreEqual(6, grid[1, 2]);
    }

    [TestMethod]
    public void RenderingCallbacksUseTrustedWrappers()
    {
        var callbacks = new RenderingCallbacks();
        var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
            .GetMethod("CallRenderingCallbacks", BindingFlags.Public | BindingFlags.Instance)!;
        var client = _plugin!.CreateClient(
            new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "test", CarName = "nfmm/radicalone", Color = default,
                        IsBot = false, IsClientPlayer = true
                    }
                ]
            },
            new RenderingContext(callbacks)
        );

        target.Invoke(client, null);
        client.End();

        Assert.AreEqual(1, callbacks.GlowResets);
        Assert.AreEqual((ushort)2, callbacks.Checkpoint);
        Assert.IsTrue(callbacks.IsFinish);
        Assert.IsTrue(callbacks.Car.CastsShadow);
        Assert.AreEqual(false, callbacks.Car.GetsShadowed);
        Assert.AreEqual(0.5f, callbacks.Car.AlphaOverride);
        Assert.AreEqual(true, callbacks.Car.Glow);
        Assert.AreEqual(false, callbacks.Car.Finish);
    }

    /// <summary>
    /// A marked interface member admits whatever implements it. The host hands the plugin a
    /// concrete clip type; without inherited visibility the call lands on that class and is
    /// refused, which made every implementer need its own marker.
    /// </summary>
    [TestMethod]
    public void SoundClipCallsSucceedThroughTheInterfaceGrant()
    {
        // The getter throws when unset, so a previous value cannot be captured. Every other test
        // method that needs a backend installs its own, so leaving the dummy is fine.
        IBackend.Backend = new DummyBackend();
        try
        {
            var target = _hostile.GetType("NFMWorld.Gamemode.Hostile.HostileGamemode", throwOnError: true)!
                .GetMethod("UseASound", BindingFlags.Public | BindingFlags.Static)!;

            // No exception: GetSound, CreateSpatialInstance, SetPosition, Loop, IsActive, Stop
            // and Dispose are all reached through the concrete DummyBackend/DummySoundClip.
            target.Invoke(null, null);
        }
        finally
        {
        }
    }

    private sealed class RenderingCallbacks : IClientCallbacks
    {
        public int GlowResets { get; private set; }
        public ushort Checkpoint { get; private set; }
        public bool IsFinish { get; private set; }
        public CarCallbacks Car { get; } = new();

        public void ResetCheckpointGlow() => GlowResets++;

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
            Checkpoint = currentCheckpoint;
            IsFinish = isFinish;
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => Car;
    }

    private sealed class RenderingContext(RenderingCallbacks callbacks) : IGamemodeContext
    {
        public BackendStage CurrentStage => throw new NotSupportedException();
        public RaceState RaceState => RaceState.InProgress;
        public IClientCallbacks ClientCallbacks => callbacks;

        public void SendServerEvent(ReadOnlySpan<byte> payload)
        {
        }

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }

    private sealed class CarCallbacks : IClientCarCallbacks
    {
        public bool CastsShadow { get; set; }
        public bool? GetsShadowed { get; set; }
        public float? AlphaOverride { get; set; }
        public bool? Glow { get; set; }
        public bool? Finish { get; set; }
    }
}
