using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Scene
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Camera _camera;
    private readonly Camera _lightCamera;
    public readonly List<IRenderable> Renderables;

    public Scene(GraphicsDevice graphicsDevice, IEnumerable<IRenderable> renderables, Camera camera, Camera lightCamera)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCamera = lightCamera;
        Renderables = [..renderables];
    }

    public Scene(GraphicsDevice graphicsDevice, ReadOnlySpan<IRenderable> renderables, Camera camera, Camera lightCamera)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCamera = lightCamera;
        Renderables = [..renderables];
    }

    public void Render(bool useShadowMapping)
    {
        _camera.OnBeforeRender();
        _lightCamera.OnBeforeRender();
        
        foreach (var renderable in Renderables)
        {
            renderable.OnBeforeRender();
        }
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // CREATE SHADOW MAP

        if (useShadowMapping)
        {
            // Set our render target to our floating point render target
            _graphicsDevice.SetRenderTarget(Program.shadowRenderTarget);

            // Clear the render target to white or all 1's
            // We set the clear to white since that represents the 
            // furthest the object could be away
            _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);

            RenderInternal(true);

            _graphicsDevice.SetRenderTarget(null);
        }

        // DRAW WITH SHADOW MAP
        
        _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        _graphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

        RenderInternal();

    }
    
    private void RenderInternal(bool isCreateShadowMap = false)
    {
        foreach (var renderable in Renderables)
        {
            renderable.Render(_camera, _lightCamera, isCreateShadowMap);
        }
    }
}