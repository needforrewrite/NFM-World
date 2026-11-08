using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;

namespace NFMWorld;

/// <summary>
/// Bakes a stage once into a top-down image, so the minimap can show the real track instead of a
/// checkpoint polyline. phy-addons bakes a flat-shaded map at load time; this renders the real
/// geometry the way the stage editor's top-down export does - an orthographic camera over the
/// scene with outlines and the environment suppressed - and crops to the course.
/// </summary>
/// <remarks>
/// A bake is a full scene render, so it cannot share a frame with the race drawing into the same
/// backbuffer. It takes the caller's command buffer, draws into an offscreen target, and restores
/// the backbuffer before returning. The image never changes for a stage, so one bake per stage is
/// all it costs. The target is cleared transparent, so the gaps between pieces let the HUD show
/// through instead of an opaque slab.
/// </remarks>
public sealed class StageMinimap : IDisposable
{
    /// <summary>Baked image resolution bounds. Sized to the stage, within these.</summary>
    private const int MinResolution = 512;

    /// <summary>
    /// 2048 is 16 MB of RGBA8 - still sharper than the radar's ~12 000-unit window needs. The
    /// old 4096 cap meant a 64 MB target allocated and rendered in one frame on stage load.
    /// </summary>
    private const int MaxResolution = 2048;

    /// <summary>World units per baked texel to aim for; the radar magnifies past this.</summary>
    private const float TargetUnitsPerTexel = 48f;

    /// <summary>Headroom around the course, so a car sitting on the bound is still inside the map.</summary>
    private const float Padding = 2500f;

    private readonly BackendStage _stage;
    private IRenderTarget? _target;
    private bool _attempted;
    private int _resolution = MinResolution;

    public IImage? Image { get; private set; }

    /// <summary>Whether a bake has been attempted. A failed one still counts, so it is not retried.</summary>
    public bool IsAttempted => _attempted;

    /// <summary>Whether there is an image to draw.</summary>
    public bool IsBaked => Image is not null;

    /// <summary>
    /// The world square the image covers on the horizontal plane. u runs along +X and v along -Z:
    /// the image's (u, v) = (0, 0) corner is <c>(MinX, MaxZ)</c>, the top-left, and its bottom
    /// edge is <see cref="MinZ"/>. Render targets store rows bottom-up on GL, which the HUD's
    /// transformed draw flips back to this order.
    /// </summary>
    public (float MinX, float MinZ, float MaxX, float MaxZ) Bounds { get; private set; }

    /// <summary>World units one image pixel covers.</summary>
    public float UnitsPerPixel => (Bounds.MaxX - Bounds.MinX) / _resolution;

    public StageMinimap(BackendStage stage)
    {
        _stage = stage;
    }

    /// <summary>
    /// Renders the map if it has not been baked yet. <paramref name="cb"/> is the caller's frame
    /// buffer; the backbuffer viewport and scissor are restored before returning.
    /// </summary>
    public void Bake(ICommandBuffer cb, int frameWidth, int frameHeight)
    {
        if (_attempted) return;
        _attempted = true;

        try
        {
            BakeCore(cb);
        }
        catch (Exception ex)
        {
            // A failed bake only costs the real map; the HUD falls back to the checkpoint lines.
            Logging.Error($"Could not bake the stage minimap: {ex.Message}");
        }
        finally
        {
            cb.SetRenderTarget(null);
            cb.SetViewport(new Viewport(0, 0, frameWidth, frameHeight));
            cb.SetScissorRect(new ScissorRect(0, 0, frameWidth, frameHeight));
        }
    }

    private void BakeCore(ICommandBuffer cb)
    {
        var pieces = _stage.Pieces.OfType<StageObject>().ToArray();
        if (pieces.Length == 0) return;

        var minX = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxZ = float.MinValue;
        foreach (var piece in pieces)
        {
            var x = (float)piece.Position.X;
            var z = (float)piece.Position.Z;
            var radius = piece.Rad.MaxRadius;
            minX = Math.Min(minX, x - radius);
            maxX = Math.Max(maxX, x + radius);
            minZ = Math.Min(minZ, z - radius);
            maxZ = Math.Max(maxZ, z + radius);
        }

        minX -= Padding;
        maxX += Padding;
        minZ -= Padding;
        maxZ += Padding;

        // The target is square, so the covered area is squared up around the centre: Bounds must
        // describe the whole image, not just the part the stage happens to use, or the radar would
        // place it at the wrong scale.
        var centerX = (minX + maxX) * 0.5f;
        var centerZ = (minZ + maxZ) * 0.5f;
        var half = Math.Max(maxX - minX, maxZ - minZ) * 0.5f;
        Bounds = (centerX - half, centerZ - half, centerX + half, centerZ + half);

        // The radar shows a 12 000-unit window in about 164 px, so anything much coarser than
        // ~70 units per texel has to be magnified and reads as blur. Sizing to the stage keeps a
        // small course sharp without forcing a huge allocation on a sprawling one.
        _resolution = Math.Clamp((int)MathF.Ceiling(half * 2 / TargetUnitsPerTexel), MinResolution, MaxResolution);
        var resolution = _resolution;
        var scale = half * 2 / resolution;

        _target = GameSparker.GraphicsDevice.CreateRenderTarget(
            new RenderTargetDesc(resolution, resolution, TextureFormat.Rgba8)
        );

        cb.SetRenderTarget(_target);
        cb.SetViewport(new Viewport(0, 0, resolution, resolution));
        cb.SetScissorRect(new ScissorRect(0, 0, resolution, resolution));
        // Transparent, not the stage ground colour: the radar sits over the HUD, so alpha 0 lets
        // the gaps between pieces show what is behind it instead of an opaque rectangle.
        cb.Clear(
            ClearOptions.Color | ClearOptions.Depth,
            new ColorRgba(0f, 0f, 0f, 0f)
        );

        var camera = new OrthoCamera { Width = resolution, Height = resolution, OrthoScale = scale };
        // Below the stage looking up, with +Z as screen up - the only pairing of position and up
        // vector that puts +X to the right AND +Z upward. Both are needed: the radar's blips map
        // larger X right and larger Z up, and laying the image out any other way drew the course
        // rotated against them. (The up vector is crossed with the view direction in CreateLookAt,
        // so flipping either one mirrors the whole map.)
        camera.PositionWithoutInterpolation = new Vector3(centerX, -50_000f, centerZ);
        camera.LookAtWithoutInterpolation = new Vector3(centerX, 0f, centerZ);
        camera.UpWithoutInterpolation = Vector3.UnitZ;
        camera.OnBeforeRender(1f);

        // The same suppressions the stage editor's top-down export uses. Outlines are the bulk of
        // the draw cost at this scale and read as noise, so they are off for the whole bake.
        var oldFadeFrom = World.FadeFrom;
        var oldOutline = World.DistantOutlineBehavior;
        // The camera sits 50 000 units up, so a player whose render distance is shorter than that
        // would have every piece culled out of the map.
        var oldRenderDistance = CameraSettings.RenderDistanceSqr;
        World.FadeFrom = 9_999_999;
        World.DistantOutlineBehavior = DistantOutlineBehavior.HideOutlines;
        CameraSettings.RenderDistanceSqr = float.MaxValue;

        // A Lighting with no cascades is what makes the shader skip shadow sampling; passing null
        // would leave the previous draw's NumCascades in the uniform and the bake would sample
        // whatever maps happen to be bound.
        var lighting = new Lighting([], [], RenderPass.Main(0));

        var queue = new RenderQueue(GameSparker.GraphicsDevice);
        try
        {
            queue.Begin(camera, lighting);
            foreach (var piece in pieces)
            {
                var mesh = GameSparker.GetStagePartMesh(piece.Rad);
                var position = (Vector3)piece.Position;
                var world = Matrix.CreateFromEuler((Euler)piece.Rotation) * Matrix.CreateTranslation(position);
                mesh.SubmitRenderables(
                    queue,
                    lighting,
                    false,
                    new Maxine.Extensions.Mathematics.BoundingSphere(position, mesh.MaxRadius),
                    RenderBucket.StagePieces,
                    world,
                    0,
                    getsShadowed: false
                );
            }

            queue.Flush(cb);
        }
        finally
        {
            queue.Dispose();
            World.FadeFrom = oldFadeFrom;
            World.DistantOutlineBehavior = oldOutline;
            CameraSettings.RenderDistanceSqr = oldRenderDistance;
        }

        cb.SetRenderTarget(null);
        Image = TheGraphics.G.WrapTexture(_target.ColorTexture);
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
        Image = null;
    }
}
