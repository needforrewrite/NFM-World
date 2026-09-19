using System;
using Maxine.Extensions.Mathematics;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld;

public class Scene : IDisposable
{
    private readonly IGraphicsDevice _graphicsDevice;
    private Camera _camera;
    private readonly IReadOnlyList<Camera> _lightCameras;
    public readonly List<GameObject> Objects;
    private readonly RenderQueue _renderQueue;
    private bool _disposed;

    public Camera ActiveCamera
    {
        get => _camera;
        set => _camera = value;
    }

    public Scene(IGraphicsDevice graphicsDevice, IEnumerable<GameObject> objects, Camera camera, IReadOnlyList<Camera> lightCameras)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCameras = lightCameras;
        Objects = [..objects];
        _renderQueue = new RenderQueue(graphicsDevice);
    }

    /// <summary>
    /// Records this scene's draws into <paramref name="cb"/> - the single command buffer the
    /// caller acquired for this frame (see <see cref="IGraphicsDevice.AcquireCommandBuffer"/>'s
    /// "one at a time" rule; this method itself never acquires or submits one).
    /// </summary>
    public void Render(ICommandBuffer cb, float alpha, bool useShadowMapping, bool clearRenderBuffer = true)
    {
        _camera.OnBeforeRender(alpha);
        foreach (var lightCamera in _lightCameras)
        {
            lightCamera.OnBeforeRender(alpha);
        }

        foreach (var renderable in Objects)
        {
            renderable.OnBeforeRender(alpha);
        }

        // TODO(Milestone 5 Stage B follow-up): the old code set BlendState.Opaque/
        // DepthStencilState.Default here as scene-wide defaults before each render element applied
        // its own state. The new IPipelineState model bakes blend/depth/rasterizer state into each
        // pipeline at creation time instead (see PipelineDesc), so there's no equivalent "set a
        // default, let each draw override it" call on ICommandBuffer - each render element's own
        // pipeline is now the single source of truth for its blend/depth state.

        var totalCascades = Math.Min(_lightCameras.Count, WorldGame.NumCascades);

        // TODO(Milestone 5 Stage B follow-up): shadow-cascade rendering needs WorldGame.
        // RebuildCascades converted off RenderTarget2D to IRenderTarget first (still stubbed from
        // Milestone 5 Stage A - see WorldGame.cs), so shadow mapping is skipped entirely for now
        // regardless of useShadowMapping.
        _ = useShadowMapping;

        if (clearRenderBuffer)
            cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(Color.CornflowerBlue.R / 255f, Color.CornflowerBlue.G / 255f, Color.CornflowerBlue.B / 255f));

        // TODO(Milestone 5 Stage B follow-up): the old code force-set all 16 sampler slots to
        // PointClamp here as a scene-wide default. ICommandBuffer has no equivalent global
        // sampler-state call - samplers are bound per-draw via SetShaderResource(slot, texture,
        // sampler) instead, so this becomes each render element's own responsibility once it binds
        // a texture (none of Line.fx's consumers do yet).

        RenderInternal(cb, RenderPass.Main(totalCascades));
    }

    private void RenderInternal(ICommandBuffer cb, RenderPass pass)
    {
        var lighting = new Lighting(_lightCameras, WorldGame.ShadowRenderTargets, pass);

        _renderQueue.Clear();

        _renderQueue.Begin(_camera, lighting);
        foreach (var obj in Objects)
        {
            obj.SubmitDraws(_renderQueue, _camera, lighting, pass);
        }

        _renderQueue.Flush(cb);
    }

    public void OnBeforeUpdate()
    {
        _camera.OnBeforeGameTick();
        foreach (var lightCamera in _lightCameras)
        {
            lightCamera.OnBeforeGameTick();
        }
    }

    public void GameTick(BackendStage currentStage)
    {
        foreach (var obj in Objects)
        {
            obj.GameTick(currentStage);
        }
    }

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _renderQueue.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
