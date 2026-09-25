// TEMPORARY. Per-draw CPU attribution, counted by the backends and read by the [DRAWPROF] block in
// WorldGame.Draw. It lives here rather than on one backend so that a single log line can compare
// two backends running the same scene - which is the only reason this exists. Remove this file, the
// increments in the backends, and that block together.
//
// Static because there is one device per process, and because the increment sites are in the
// backends while the reader is the application assembly, which cannot see them. Unconditional
// increments rather than a guarded flag: a branch on the measured path would perturb what it
// measures, and these are a few `inc` instructions against a graphics call.
namespace NFMWorld.Graphics;

public static class DrawProfiler
{
    /// <summary>Per-call counters, for attributing a frame's draw volume.</summary>
    public static long CountSetPipeline;
    public static long CountSetUniform;
    public static long CountSetVertexBuffer;
    public static long CountDraw;

    /// <summary>
    /// Cumulative <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> deltas for the three
    /// sub-steps of a draw, so the frame's CPU cost can be attributed between uniform upload,
    /// vertex-attribute re-pointing and the draw call itself. Ticks rather than microseconds so the
    /// accumulation stays integer; divide by the frequency when reporting.
    /// </summary>
    public static long UniformTicks;
    public static long AttribTicks;
    public static long DrawCallTicks;

    /// <summary>
    /// Ticks spent inside <see cref="ICommandBuffer.Draw"/> and its three siblings, i.e. wall time
    /// in the backend rather than in the game's own recording code.
    ///
    /// This exists because <c>sceneUs</c> alone is ambiguous: it times the whole scene phase, so a
    /// backend that defers work to draw time (sokol validates and re-binds a full <c>sg_bindings</c>
    /// per draw, and flushes its CPU mirrors there) reports a large number that says more about the
    /// backend than about the scene. Subtracting this from <c>sceneUs</c> is what separates the two.
    /// </summary>
    public static long DrawCallWallTicks;

    /// <summary>
    /// Number of draws whose <c>sg_apply_pipeline</c> left no pipeline bound, so the draw itself did
    /// nothing.
    ///
    /// sokol's validation layer reports a rejected pipeline by <em>logging</em>, not by the return
    /// value or an exception, and every following call is then skipped by its own "must be called
    /// after sg_apply_pipeline" check. So a frame can show hundreds of draws and produce no output
    /// with nothing raised anywhere. Zero is the only reading on this counter that means the draws
    /// it counted actually happened.
    /// </summary>
    public static long CountDrawWithNoPipeline;

    /// <summary>
    /// Times <paramref name="draw"/> and adds the elapsed ticks to <see cref="DrawCallWallTicks"/>.
    /// Every backend wraps its four draw entry points in this, so the number means the same thing
    /// on each and <c>sceneUs - drawCallWallUs</c> is a like-for-like comparison.
    /// </summary>
    public static void TimeDraw(Action draw)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        draw();
        DrawCallWallTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
    }

    public static void Reset()
    {
        CountSetPipeline = 0;
        CountSetUniform = 0;
        CountSetVertexBuffer = 0;
        CountDraw = 0;
        UniformTicks = 0;
        AttribTicks = 0;
        DrawCallTicks = 0;
        DrawCallWallTicks = 0;
        CountDrawWithNoPipeline = 0;
    }
}
