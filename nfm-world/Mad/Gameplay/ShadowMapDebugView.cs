using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using NFMWorld.Graphics;

namespace NFMWorld.Gameplay;

/// <summary>
/// The shadow-cascade debug overlay, shown while <see cref="BaseStageRenderingPhase.DebugDisplay"/>
/// is on (<c>r_debugdisplay</c> in the dev console). This is the feature the migration had to drop
/// rather than convert: it used to be a <c>SpriteBatch</c> blit of <c>WorldGame.ShadowRenderTargets</c>
/// straight into the backbuffer, and neither SpriteBatch nor an XNA device exists any more.
/// </summary>
/// <remarks>
/// A cascade is a 32-bit float depth texture (<see cref="TextureFormat.Single"/>), which cannot be
/// handed to ImGui as-is - sampling it as RGBA8 reads the float bits, not depth. So each capture
/// reads the cascade back on the CPU (<see cref="IGraphicsDevice.ReadTexture"/> - a GPU stall by
/// design), remaps float depth to 8-bit grey, and uploads that into an <see cref="TextureFormat.Rgba8"/>
/// texture ImGui can display. That is far too expensive to do every frame for every cascade, so one
/// cascade is captured per <see cref="CaptureIntervalFrames"/> frames, round-robin - the window
/// labels which cascade it is currently showing.
/// </remarks>
internal static class ShadowMapDebugView
{
    private const int PreviewSize = 256;
    private const int CaptureIntervalFrames = 5;

    private static ITexture? _previewTexture;
    private static ImTextureRef _previewRef;
    private static bool _bound;
    private static byte[]? _readbackScratch;
    private static readonly byte[] PreviewPixels = new byte[PreviewSize * PreviewSize * 4];

    private static int _framesSinceCapture;
    private static int _nextCascade;
    private static int _shownCascade;

    /// <summary>
    /// Reads one cascade back and refreshes the preview texture. Call once per frame from a phase's
    /// render pass, with the backbuffer bound (the cascade textures must not be bound while read).
    /// </summary>
    public static void Capture(ICommandBuffer cb)
    {
        var targets = WorldGame.ShadowRenderTargets;
        if (targets.Count == 0) return;

        if (_framesSinceCapture++ < CaptureIntervalFrames) return;
        _framesSinceCapture = 0;

        var cascade = _nextCascade % targets.Count;
        _nextCascade++;
        if (targets[cascade] is not { } target) return;

        var resolution = WorldGame.ShadowResolution;
        var byteCount = resolution * resolution * sizeof(float);
        if (_readbackScratch is null || _readbackScratch.Length < byteCount)
            _readbackScratch = new byte[byteCount];

        GameSparker.NewGraphicsDevice.ReadTexture(
            target.ColorTexture, 0, 0, resolution, resolution,
            _readbackScratch.AsSpan(0, byteCount));

        // Nearest-neighbour downsample, float depth -> grey. Unwritten texels hold the cascades'
        // white clear (depth 1.0), so "lit" reads as white and geometry shows as a grey silhouette.
        var depths = MemoryMarshal.Cast<byte, float>(_readbackScratch.AsSpan(0, byteCount));
        var step = Math.Max(1, resolution / PreviewSize);
        for (var y = 0; y < PreviewSize; y++)
        {
            var row = (y * step) * resolution;
            for (var x = 0; x < PreviewSize; x++)
            {
                var source = row + x * step;
                var depth = source < depths.Length ? depths[source] : 0f;
                var grey = float.IsFinite(depth) ? (byte)(Math.Clamp(depth, 0f, 1f) * 255f) : (byte)0;
                var destination = (y * PreviewSize + x) * 4;
                PreviewPixels[destination] = grey;
                PreviewPixels[destination + 1] = grey;
                PreviewPixels[destination + 2] = grey;
                PreviewPixels[destination + 3] = 255;
            }
        }

        EnsureTexture();
        cb.UpdateTexture(_previewTexture!, 0, 0, PreviewSize, PreviewSize, PreviewPixels);
        _shownCascade = cascade;
    }

    public static void DrawImgui()
    {
        if (!_bound) return;

        var windowSize = new System.Numerics.Vector2(PreviewSize + 24, PreviewSize + 64);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Once);
        if (!ImGui.Begin("Shadow cascades"))
            return;

        ImGui.TextDisabled($"cascade {_shownCascade} of {WorldGame.ShadowRenderTargets.Count}, " +
                           $"{WorldGame.ShadowResolution}x{WorldGame.ShadowResolution} {TextureFormat.Single}");
        // Flip V: cascade render targets are stored bottom-up, same as the stage editor's part
        // thumbnails (see StageEditorPhase.Panels.cs).
        ImGui.Image(_previewRef, new System.Numerics.Vector2(PreviewSize, PreviewSize),
            new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
        ImGui.End();
    }

    private static void EnsureTexture()
    {
        if (_previewTexture is not null) return;

        _previewTexture = GameSparker.NewGraphicsDevice.CreateTexture(
            new TextureDesc(PreviewSize, PreviewSize, TextureFormat.Rgba8), PreviewPixels);
        if (WorldGame.ImguiRenderer is { } imgui)
        {
            _previewRef = imgui.BindTexture(_previewTexture);
            _bound = true;
        }
    }

    /// <summary>Releases the preview texture. Called from <see cref="WorldGame.Dispose"/> - it is held in a static, so nothing else would ever dispose it and the backend's leak tracker would flag it at shutdown.</summary>
    public static void Dispose()
    {
        if (_previewTexture is null) return;

        if (_bound && WorldGame.ImguiRenderer is { } imgui)
            imgui.UnbindTexture(_previewRef);

        _previewTexture.Dispose();
        _previewTexture = null;
        _bound = false;
    }
}
