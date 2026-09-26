// LLM maintained.
//
// A disk cache for D3DCompile's output, keyed by the shader source and the profile.
//
// This exists for one shader. Compiling every stage of this tree's bundles costs about 7.7 seconds
// on a cold start, and 7.6 of those are apos_shapesSpriteBatch's pixel stage - 452 KB of generated
// HLSL that fxc spends most of its time optimizing down to a 1.4 MB blob. The other seventeen stages
// together cost ~120 ms, which is not worth a cache on its own, but the shape shader alone is enough
// to make every launch feel like it is hanging.
//
// The key is a hash of the HLSL itself rather than of the file it came from, and that is the whole
// design: a shader edit changes the source, which changes the key, which misses. There is no
// invalidation step to forget and no way for a stale blob to be handed to a shader that has since
// changed - which is the failure mode a timestamp-based key would have, since the generated bundles
// live under obj/ where a rebuild can rewrite them without moving a timestamp the running app knows
// about.
//
// Safe by construction on the other side too: D3DCompile is a pure function of its input at a given
// d3dcompiler version, so a cache hit returns byte-identical DXBC to what the compile would have
// produced. Nothing here changes what the GPU is handed, only how long it takes to get it.
//
// Reads and writes are best-effort throughout. A cache that cannot be read is a cache miss, and one
// that cannot be written costs a recompile - neither is a reason to fail a launch, and a shader that
// genuinely will not compile still throws from the compiler below with its own message.
using System.Security.Cryptography;
using System.Text;

namespace NFMWorld.Graphics.D3D11;

/// <summary>
/// Reads and writes compiled shader blobs under a host-supplied directory.
/// </summary>
/// <remarks>
/// An instance owned by the device rather than a static, so that whether the cache is on is a
/// property of the device that asked for it. That mirrors the rest of this backend - the device owns
/// what it creates - and it keeps a device created with the cache disabled (the smoke test's) from
/// reading output a previous run left behind, which is the whole point of disabling it there.
/// <para>
/// The location itself is derived here rather than passed in, because there is no host-side
/// convention to inherit: the application's own data directory is its business, and a backend that
/// hardcoded <c>data/cfg</c> would break the moment anything else hosted it.
/// </para>
/// </remarks>
internal sealed class D3D11ShaderCache
{
    /// <summary>
    /// A one-byte version stamped into every entry's header.
    ///
    /// Bumping this invalidates every cached blob at once, which is the escape hatch for a change that
    /// alters the compiler's *output* without altering its input - a new d3dcompiler, or a change to
    /// <see cref="D3D11ShaderCompiler.CompileFlags"/>. The key below already covers the source,
    /// profile and compiler version; this covers everything else, including the possibility that a
    /// future change here is wrong about what the key needs to include.
    /// </summary>
    private const byte FormatVersion = 1;

    /// <summary>
    /// The magic an entry starts with, so a truncated or foreign file is rejected rather than parsed.
    /// Four bytes of the format's name, and no attempt at a checksum - the hash in the filename guards
    /// against reading the wrong entry, and the length check guards against a partial write.
    /// </summary>
    private static ReadOnlySpan<byte> Magic => "DXBC"u8;

    private readonly string _directory;

    private D3D11ShaderCache(string directory) => _directory = directory;

    /// <summary>
    /// The cache the device should use, or null when it should compile every shader itself.
    /// </summary>
    internal static D3D11ShaderCache? Create(bool enabled)
    {
        if (!enabled)
            return null;

        var shaderCacheDir = Path.Combine(AppContext.BaseDirectory, "data", "cfg", "shadercache");
        Directory.CreateDirectory(shaderCacheDir);
        return new D3D11ShaderCache(shaderCacheDir);
    }

    /// <summary>
    /// Where entries are kept, for the diagnostic line the device prints at startup.
    ///
    /// Deliberately not called <c>Directory</c> or <c>Path</c>, either of which shadows the
    /// <see cref="System.IO"/> type of the same name for every unqualified use inside this class - so
    /// the <c>CreateDirectory</c> and <c>Path.Combine</c> calls below would resolve against this
    /// property instead of the framework, with errors that name an unrelated ACL extension method and
    /// a missing <c>Combine</c>.
    /// </summary>
    internal string Location => _directory;

    /// <summary>
    /// The cache's filename for a source blob: the hash of everything that determines the compile's
    /// output, rendered as hex.
    /// </summary>
    /// <remarks>
    /// The profile and the compiler version are hashed alongside the source rather than encoded in the
    /// name, so there is one opaque filename per distinct compile and no parsing to get wrong. The
    /// compiler version is included because the same source compiles to different DXBC under a
    /// different d3dcompiler - a real scenario here, since this backend binds whatever
    /// <c>d3dcompiler_47.dll</c> the machine has.
    /// </remarks>
    private static string KeyFor(string hlsl, string profile)
    {
        // A stack-allocated hash target rather than letting the incremental hash allocate: SHA256's
        // output is 32 bytes, and this runs once per stage per launch. The incremental API is no more
        // code than the one-shot overload and avoids copying the source into a concatenated buffer.
        Span<byte> hash = stackalloc byte[32];
        var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            hasher.AppendData(Magic);
            hasher.AppendData(Encoding.UTF8.GetBytes(hlsl));
            hasher.AppendData(Encoding.UTF8.GetBytes(profile));
            hasher.AppendData(Encoding.UTF8.GetBytes(D3D11ShaderCompiler.CompilerVersion));
            hasher.GetHashAndReset(hash);
        }
        finally
        {
            hasher.Dispose();
        }

        return Convert.ToHexStringLower(hash) + ".dxbc";
    }

    /// <summary>
    /// Returns the cached blob for this source, or null on any miss.
    ///
    /// Every failure below the top is a miss rather than an exception, for the reason the file comment
    /// gives: the cache is an optimisation, and one that cannot read its own storage should cost a
    /// recompile, not a launch.
    /// </summary>
    internal byte[]? TryRead(string hlsl, string profile)
    {
        try
        {
            var path = Path.Combine(_directory, KeyFor(hlsl, profile));
            if (!File.Exists(path))
                return null;

            var bytes = File.ReadAllBytes(path);

            // Header: the magic, the format version, then the payload. A file too short to hold the
            // header is one written by something else, or truncated by a crash mid-write.
            if (bytes.Length <= Magic.Length + 1 || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                return null;

            if (bytes[Magic.Length] != FormatVersion)
                return null;

            return bytes[(Magic.Length + 1)..];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            GraphicsDiagnostics.Warning?.Invoke(
                $"The shader cache at {_directory} could not be read ({e.GetType().Name}: {e.Message}); " +
                "recompiling.");
            return null;
        }
    }

    /// <summary>
    /// Writes a compiled blob for next launch, if it is not already there.
    /// </summary>
    /// <remarks>
    /// Written to a process-unique temporary name and then moved into place, so a crash or a second
    /// instance can never leave a half-written entry that a later launch would read as a valid blob.
    /// The move is the atomic rename, and it overwrites - which matters because two instances racing to
    /// compile the same shader will both reach here, and the second must not fail for having lost.
    /// </remarks>
    internal void Store(string hlsl, string profile, byte[] dxbc)
    {
        try
        {
            Directory.CreateDirectory(_directory);

            var path = Path.Combine(_directory, KeyFor(hlsl, profile));
            if (File.Exists(path))
                return;

            var payload = new byte[Magic.Length + 1 + dxbc.Length];
            Magic.CopyTo(payload);
            payload[Magic.Length] = FormatVersion;
            dxbc.CopyTo(payload, Magic.Length + 1);

            var temporary = path + "." + Environment.ProcessId + ".tmp";
            File.WriteAllBytes(temporary, payload);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            GraphicsDiagnostics.Warning?.Invoke(
                $"The shader cache at {_directory} could not be written ({e.GetType().Name}: {e.Message}); " +
                "the shader will be recompiled next launch.");
        }
    }
}
