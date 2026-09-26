// LLM maintained.
//
// A disk cache of linked GL program binaries, keyed by the GLSL that produced them.
//
// This exists because of one shader. The apos-shapes pixel program takes around two minutes to link
// under ANGLE - measured, see the smoke test's MeasureAposCompile - against 15 ms for every other
// program in the tree combined, and it is recompiled from scratch on every launch. ANGLE hands back
// its translated form through OES_get_program_binary, so a launch that finds a valid binary never
// pays that cost at all.
//
// Why this is a separate type rather than a few lines in GlShaderProgram: the capability is an
// optional extension, the cache can be disabled for at least three independent reasons (no
// extension, no writable directory, a binary this driver refuses), and that fallback policy deserves
// to be readable in one place. GlBaseVertexDraw is the same shape for the same reason.
//
// The key is the GLSL text plus the driver's identity, not the source file's path or timestamp.
// Binaries are explicitly not portable between drivers - the OES spec says a program binary "may
// become invalid after a driver update" - so anything that identifies the driver has to be part of
// what was cached, or a driver change would hand back a binary the new driver must reject.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NFMWorld.Shaders;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.OES;

namespace NFMWorld.Graphics.OpenGL;

/// <summary>
/// The program-binary cache for one GL context, or a disabled one when the context cannot support it.
/// </summary>
internal sealed class GlProgramCache
{
    private readonly GL _gl;
    private readonly OesGetProgramBinary? _ext;
    private readonly string _directory;
    private readonly string _prefix;

    /// <summary>
    /// Whether this cache can save and load at all. False is a normal state, not an error: it means
    /// every program is compiled from source, which is exactly what happened before this existed.
    /// </summary>
    internal bool IsEnabled => _ext is not null;

    private GlProgramCache(GL gl, OesGetProgramBinary? ext, string directory, string prefix)
    {
        _gl = gl;
        _ext = ext;
        _directory = directory;
        _prefix = prefix;
    }

    /// <summary>
    /// Resolves the cache for <paramref name="gl"/>.
    ///
    /// <paramref name="directory"/> is created if missing; a failure to create it disables the cache
    /// rather than throwing, because a read-only or full data directory is a reason to be slower, not
    /// a reason not to boot. The directory is not a parameter the caller has to get right - the
    /// backend defaults it beside the game's other per-user state.
    /// </summary>
    internal static GlProgramCache Resolve(GL gl, string directory)
    {
        // Probed rather than assumed from the extension string: an advertised extension whose entry
        // points do not resolve is exactly the case TryGetExtension exists to catch.
        if (!gl.TryGetExtension(out OesGetProgramBinary ext))
            return new GlProgramCache(gl, null, directory, "disabled");

        // The driver identity is read once and folded into every key: the same GLSL compiled by a
        // different driver must not hit the same cache entry, and this is what makes that true even
        // if the binary format the driver advertises is identical, which it usually is.
        var prefix = $"{DriverIdentity(gl)}-{gl.GetInteger((GLEnum)OES.ProgramBinaryFormatsOes):X}";

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception)
        {
            // A read-only or full data directory is a reason to be slower, not a reason not to boot.
            return new GlProgramCache(gl, null, directory, prefix);
        }

        return new GlProgramCache(gl, ext, directory, prefix);
    }

    /// <summary>
    /// A stable string naming the driver and its version.
    ///
    /// GL_VERSION is in here alongside the renderer string because it is the one that changes on a
    /// driver update, which is the event the OES spec names as invalidating a binary.
    /// </summary>
    private static string DriverIdentity(GL gl) => string.Join('|',
        String(gl, StringName.Vendor),
        String(gl, StringName.Renderer),
        String(gl, StringName.Version));

    private static unsafe string String(GL gl, StringName name)
    {
        var pointer = gl.GetString(name);
        return pointer is null ? "?" : Marshal.PtrToStringUTF8((nint)pointer) ?? "?";
    }

    /// <summary>
    /// The cache file for a vertex/pixel pair, or null when the pair cannot be keyed.
    ///
    /// Hashed over the exact text handed to the driver - the dialect this backend actually compiles -
    /// so a shader edit misses the cache and a whitespace-only touch of the .fx file cannot.
    /// </summary>
    private string? PathFor(ShaderStageSources vertex, ShaderStageSources pixel)
    {
        if (!IsEnabled)
            return null;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{_prefix}\n--vertex--\n{vertex.GlslEs}\n--pixel--\n{pixel.GlslEs}\n"));

        return Path.Combine(_directory, $"{Convert.ToHexString(hash)}.bin");
    }

    /// <summary>
    /// The cached binary for this pair, or null when there is none, it cannot be read, or the driver
    /// rejects it.
    ///
    /// This is the whole reason the cache is worth having: a hit means the caller never compiles or
    /// links. A rejected binary is treated as a miss rather than an error - the driver is the
    /// authority on whether a binary is still valid, and it says so by failing the load.
    /// </summary>
    internal unsafe uint? TryLoad(ShaderStageSources vertex, ShaderStageSources pixel)
    {
        if (PathFor(vertex, pixel) is not { } path || _ext is null)
            return null;

        byte[] blob;
        try
        {
            blob = File.ReadAllBytes(path);
        }
        catch (Exception)
        {
            return null;
        }

        if (blob.Length == 0)
            return null;

        var program = _gl.CreateProgram();

        // The format is not stored alongside the blob: it is whatever this context advertised under
        // the same identity, and a driver that changed its format has already changed the key.
        var format = (OES)_gl.GetInteger((GLEnum)OES.ProgramBinaryFormatsOes);
        _ext.ProgramBinary(program, format, blob.AsSpan());

        var status = 0;
        _gl.GetProgram(program, GLEnum.LinkStatus, &status);
        if (status == 0)
        {
            // The spec is explicit that the prior state is lost when a load fails, so this program is
            // discarded outright. The caller recompiles from source into a fresh one - retrying the
            // load, or linking this object, would fail the same way.
            _gl.DeleteProgram(program);
            return null;
        }

        return program;
    }

    /// <summary>
    /// Writes the binary for a freshly linked program, so the next launch can skip the compile.
    ///
    /// Best-effort throughout: a write that fails leaves the program working and the next launch
    /// slow, which is the state the tree is in today anyway.
    /// </summary>
    internal unsafe void TrySave(uint program, ShaderStageSources vertex, ShaderStageSources pixel)
    {
        if (PathFor(vertex, pixel) is not { } path || _ext is null)
            return;

        var length = 0;
        _gl.GetProgram(program, (GLEnum)OES.ProgramBinaryLengthOes, &length);
        if (length <= 0)
            return;

        var blob = new byte[length];
        uint written = 0;
        var format = default(OES);   // overwritten by the call
        _ext.GetProgramBinary(program, &written, &format, blob.AsSpan());
        if (written == 0)
            return;

        try
        {
            // Written aside and moved into place so a run interrupted mid-write - which is likely
            // here, since the compile it follows takes minutes and a player may well close the
            // window - cannot leave a truncated binary that the next launch would have to reject.
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, blob.AsSpan(0, (int)written).ToArray());
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception)
        {
            // Deliberately swallowed - see this method's docs.
        }
    }
}
