using TightPack;
using System.Runtime.CompilerServices;
using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;

[assembly: Gamemode(Name = "Hostile", Description = "Attempts everything the sandbox should stop.")]

namespace NFMWorld.Gamemode.Hostile;

/// <summary>
/// Probes the plugin sandbox: each method reaches for something a plugin has no business
/// reaching for. <see cref="PluginSandboxTests"/> loads this assembly the way the game does
/// and asserts every probe fails.
/// </summary>
public class HostileGamemode : PluginGamemode
{
    public HostileGamemode(
        ClientGamemodeParameters parameters,
        IGamemodeContext context,
        string gamemodeId,
        Dictionary<string, object>? config = null
    )
        : base(parameters, context, gamemodeId, config)
    {
    }

    /// <summary>Reads a file relative to the game's working directory.</summary>
    public static string ReadAFile() => File.ReadAllText("data/config.json");

    /// <summary>Writes one.</summary>
    public static void WriteAFile() => File.WriteAllText("pwned.txt", "pwned");

    /// <summary>Opens a socket.</summary>
    public static object OpenASocket()
    {
        using var client = new System.Net.Sockets.TcpClient();
        return client.Connected;
    }

    /// <summary>
    /// Calls a host helper that is part of the host's own logic rather than its plugin
    /// surface. The type is public and reachable, so the only thing standing between a
    /// plugin and this is the policy itself.
    /// </summary>
    public static int CallAHiddenMember()
        => NFMWorldLibrary.Util.NaturalCompare.CompareNatural("a", "b");

    /// <summary>Loads a native library.</summary>
    public static void LoadNative()
        => System.Runtime.InteropServices.NativeLibrary.Load("kernel32");

    /// <summary>Allocates memory that is never zeroed, then relies on that.</summary>
    public static unsafe int UninitializedMemory()
        => GC.AllocateUninitializedArray<byte>(8)[0];

    /// <summary>Reinterprets a narrower value as a wider one, reading past its end.</summary>
    public static unsafe void Reinterpret(ref int value)
    {
        ref var wide = ref System.Runtime.CompilerServices.Unsafe.As<int, long>(ref value);
        wide = 0;
    }

    /// <summary>
    /// Reads a private field of a host type that is marked [PluginVisible] with no
    /// visibility argument. Such a type is visible through its marked members only, so
    /// this field is out of reach however the plugin spells the access.
    /// </summary>
    public static bool ReadAPrivateField()
    {
        var keys = default(Keys);
        return LowBits(ref keys) != UInt128.Zero;
    }

    // Compiles to a direct ldfld of the private field, which is the token the policy has
    // to refuse. InternalsVisibleTo would not help: that grants internal, not private.
    // UnsafeAccessor only lets the probe name the field.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lowBits")]
    private static extern ref UInt128 LowBits(ref Keys keys);

    /// <summary>Serializes a plugin event, which runs the generated TightPack formatter.</summary>
    public static byte[] SerializeAnEvent() => TightPackSerializer.Serialize(new HostileEvent(42));

    /// <summary>
    /// Serializes an event whose members resolve formatters the generated code names itself:
    /// a collection, a multi-dimensional array, a set and a <see cref="Type"/>.
    /// </summary>
    public static byte[] SerializeACollectionEvent()
    {
        var value = new HostileCollectionEvent
        {
            Stack = new Stack<int>(new[] { 1, 2 }),
            Map = new SortedDictionary<int, string> { [1] = "a" },
            Set = new HashSet<int> { 5 },
            Grid = new[,] { { 1, 2, 3 }, { 4, 5, 6 } },
            // A corelib type: TypeFormatter cannot resolve a name from this collectible load context.
            Kind = typeof(int),
        };

        return TightPackSerializer.Serialize(value);
    }

    /// <summary>Serializes and deserializes the same event, exercising both directions of the wire format.</summary>
    public static int[,] RoundTripACollectionEvent()
    {
        var payload = SerializeACollectionEvent();
        var restored = TightPackSerializer.Deserialize<HostileCollectionEvent>(payload);
        return restored.Grid!;
    }

    public void CallRenderingCallbacks()
    {
        var callbacks = ClientCallbacks;
        callbacks.ResetCheckpointGlow();
        callbacks.UpdateCheckpointGlow(2, true);
        var car = callbacks.GetClientCarCallbacks(null!);
        car.CastsShadow = true;
        car.GetsShadowed = false;
        car.AlphaOverride = 0.5f;
        car.Glow = true;
        car.Finish = false;
    }
}

/// <summary>A plugin event, to exercise the generated TightPack formatter path.</summary>
[TightPackable]
public partial record HostileEvent(int Value);

/// <summary>
/// The same path for a member whose formatter the generated code names itself, which is the part
/// of the provider surface a plugin reaches directly rather than through the writer.
/// </summary>
[TightPackable]
public partial class HostileCollectionEvent
{
    public Stack<int>? Stack { get; set; }
    public SortedDictionary<int, string>? Map { get; set; }
    public ISet<int>? Set { get; set; }
    public int[,]? Grid { get; set; }
    public Type? Kind { get; set; }
}
