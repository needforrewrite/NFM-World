using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpSokol.Native;

namespace NFMWorld.Graphics.Sokol;

/// <summary>
/// The <c>sg_logger</c> this backend installs, and the log-level vocabulary it reports in.
///
/// A logger is not optional for this backend, and the reason is in sokol's design rather than in
/// this app's: sokol reports a validation failure by <em>logging</em>, not by failing the call that
/// caused it. <c>_SG_VALIDATE</c> only sets an error flag and logs (<c>sokol_gfx.h:7779</c>), so a
/// rejected draw, a mismatched input layout or an exhausted pool is a completely silent
/// no-op-cum-warning without one. That is exactly the class of bug this backend is most likely to
/// hit first - the D3D11 input layout is built from <see cref="SokolMapping.SemanticName"/>'s
/// semicolon-joined name/index pairs and is rejected by D3D11 rather than by sokol if any of them
/// disagrees with the shader signature - so a run with no logger is a run that cannot tell a
/// working frame from a silently-discarded one.
///
/// The callback is a <c>static readonly</c> field holding a function pointer, so it is rooted for
/// the life of the process and cannot be collected out from under sokol. <c>sg_desc</c> stores only
/// the pointer (<c>sokol_gfx.h:5435-5445</c>), and nothing in the API offers a way to uninstall one.
/// </summary>
internal static unsafe class SokolLog
{
    /// <summary>sokol's log levels, from <c>sokol_log.h:257-260</c> (which spells "panic" out as case 0).</summary>
    private const uint LevelPanic = 0;
    private const uint LevelError = 1;
    private const uint LevelWarning = 2;

    /// <summary>
    /// <c>_SG_LOGITEM_VALIDATE_APIP_PIPELINE_VALID</c> - "sg_apply_pipeline: the pipeline object is
    /// not in valid state", sokol_gfx.h:5110. Counted because it is the one failure that makes a
    /// draw a silent no-op rather than a partially-wrong one: sokol's validation continues to the
    /// end of <c>sg_apply_pipeline</c> and logs, while the backend call that would have bound
    /// anything is behind a <c>if (pip->id != SG_INVALID_ID)</c>, so a rejected pipeline leaves the
    /// state exactly as it was and every following uniform and binding call is skipped by its own
    /// "must be called after sg_apply_pipeline" check. A frame can then report its full draw count
    /// and produce nothing at all, with no return value or exception saying so - which is why the
    /// counter exists rather than an assumption that the draws ran.
    /// </summary>
    private const uint ItemApplyPipelinePipelineValid = 30;

    /// <summary>
    /// The logger for <c>sg_desc.logger</c>. Cast to the generated
    /// <c>delegate* unmanaged[Cdecl]</c> type at the assignment site.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void Func(
        sbyte* tag,
        uint logLevel,
        uint logItem,
        sbyte* message,
        uint lineNr,
        sbyte* filename,
        void* userData)
    {
        // Level 3 is sokol's "info", which the library emits for nothing on this path. Dropping it
        // rather than filtering at the call site keeps the hot path free of string building.
        if (logLevel >= 3) return;

        // Counted before the message is built, and left uncounted in release builds only because
        // sokol's own validation is what raises it - see ItemApplyPipelinePipelineValid.
        if (logItem == ItemApplyPipelinePipelineValid && logLevel == LevelError)
            DrawProfiler.CountDrawWithNoPipeline++;

        var level = logLevel switch
        {
            LevelPanic => "panic",
            LevelError => "error",
            _ => "warning",
        };

        // The item's identifier is deliberately not resolved through sg_log_item: SharpSokol emits
        // the 548-item enum as compile-time C# constants, so there is no runtime table to index and
        // casting an arbitrary uint into it would be a lie whenever the native library and this
        // binding drift apart - which is precisely when a log matters most. The numeric id is more
        // useful here, because sokol_gfx.h's _SG_LOGITEM_XMACRO list is indexed by it
        // (sokol_gfx.h:4704).
        var line =
            $"[sokol/{level}] item {logItem}: {Utf8(message)} " +
            $"({Utf8(filename)}:{lineNr}{TagSuffix(tag)})";

        // Written to stderr rather than through the app's Logging facade: this backend does not
        // reference NFMWorld.Library (nor should it - that is the application's logging stack,
        // pulling in Sentry and the rolling-file provider, and this assembly is a graphics backend).
        // Stderr is also the more robust choice here, because sokol's logger is a native callback
        // and a panic can arrive at a moment when the application's logging pipeline is not in a
        // state to accept one.
        Console.Error.WriteLine(line);
    }

    private static string TagSuffix(sbyte* tag) => tag is null ? string.Empty : $", {Utf8(tag)}";

    /// <summary>
    /// A NUL-terminated UTF-8 string sokol owns, or <c>&lt;null&gt;</c>. Empty rather than null is
    /// possible too - sokol passes "" for an absent tag - so both are collapsed to one spelling.
    /// </summary>
    private static string Utf8(sbyte* value) =>
        value is null || *value == 0 ? "<null>" : Marshal.PtrToStringUTF8((IntPtr)value) ?? "<invalid>";
}
