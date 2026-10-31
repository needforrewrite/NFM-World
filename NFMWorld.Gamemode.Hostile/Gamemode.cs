using MemoryPack;
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
        Dictionary<string, object>? config = null)
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

    /// <summary>Serializes a plugin event, which runs the generated MemoryPack formatter.</summary>
    public static byte[] SerializeAnEvent() => MemoryPackSerializer.Serialize(new HostileEvent(42));
}

/// <summary>A plugin event, to exercise the generated MemoryPack formatter path.</summary>
[MemoryPackable]
public partial record HostileEvent(int Value);
