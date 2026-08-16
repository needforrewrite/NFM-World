using System;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class Scene : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
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

    public Scene(GraphicsDevice graphicsDevice, IEnumerable<GameObject> objects, Camera camera, IReadOnlyList<Camera> lightCameras)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCameras = lightCameras;
        Objects = [..objects];
        _renderQueue = new RenderQueue(graphicsDevice);
    }

    public void Render(float alpha, bool useShadowMapping, bool clearRenderBuffer = true)
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

        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        var totalCascades = Math.Min(_lightCameras.Count, WorldGame.NumCascades);

        // CREATE SHADOW MAP
        if (useShadowMapping)
        {
            for (var cascade = 0; cascade < totalCascades; cascade++)
            {
                _graphicsDevice.SetRenderTarget(WorldGame.ShadowRenderTargets[cascade]);
                _graphicsDevice.Clear(Color.White);

                RenderInternal(RenderPass.Shadow(cascade, totalCascades));
            }

            _graphicsDevice.SetRenderTarget(null);
        }

        // DRAW WITH SHADOW MAP
        if (clearRenderBuffer)
            _graphicsDevice.Clear(Color.CornflowerBlue);

        for (var i = 0; i < 16; i++)
            _graphicsDevice.SamplerStates[i] = SamplerState.PointClamp;

        RenderInternal(RenderPass.Main(totalCascades));
    }

    private void RenderInternal(RenderPass pass)
    {
        var lighting = new Lighting(_lightCameras, WorldGame.ShadowRenderTargets, pass);

        _renderQueue.Clear();
        
        _renderQueue.Begin(_camera, lighting);
        foreach (var obj in Objects)
        {
            obj.SubmitDraws(_renderQueue, _camera, lighting, pass);
        }

        _renderQueue.Flush();
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