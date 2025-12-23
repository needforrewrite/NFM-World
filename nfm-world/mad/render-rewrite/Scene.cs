using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class ListRenderable(IReadOnlyList<IRenderable?> renderables) : IRenderable
{
    public void Render(Camera camera, Lighting? lighting)
    {
        foreach (var renderable in renderables)
        {
            if (renderable == null)
            {
                Console.WriteLine("Null renderable in ListRenderable. Please fix!");
            }
            else
            {
                renderable.Render(camera, lighting);
            }
        }
    }
}

public class Scene
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Camera _camera;
    private readonly Camera[] _lightCameras;
    public readonly List<IRenderable> Renderables;

    public Scene(GraphicsDevice graphicsDevice, IEnumerable<IRenderable> renderables, Camera camera, Camera[] lightCameras)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCameras = lightCameras;
        Renderables = [..renderables];
    }

    public Scene(GraphicsDevice graphicsDevice, ReadOnlySpan<IRenderable> renderables, Camera camera, Camera[] lightCameras)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCameras = lightCameras;
        Renderables = [..renderables];
    }

    public void Render(bool useShadowMapping)
    {
        _camera.OnBeforeRender();
        foreach (var lightCamera in _lightCameras)
        {
            lightCamera.OnBeforeRender();
        }
        
        foreach (var renderable in Renderables)
        {
            renderable.OnBeforeRender();
        }
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // CREATE SHADOW MAP

        if (useShadowMapping)
        {
            for (var cascade = 0; cascade < Math.Min(_lightCameras.Length, Program.shadowRenderTargets.Length); cascade++)
            {
                // Set our render target to our floating point render target
                _graphicsDevice.SetRenderTarget(Program.shadowRenderTargets[cascade]);

                // Clear the render target to white or all 1's
                // We set the clear to white since that represents the 
                // furthest the object could be away
                _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);

                RenderInternal(true, cascade);
            }
            
            _graphicsDevice.SetRenderTarget(null);
        }

        // DRAW WITH SHADOW MAP
        
        _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        _graphicsDevice.SamplerStates[1] = SamplerState.PointClamp;
        _graphicsDevice.SamplerStates[2] = SamplerState.PointClamp;
        _graphicsDevice.SamplerStates[3] = SamplerState.PointClamp;

        RenderInternal();

    }
    
    private void RenderInternal(bool isCreateShadowMap = false, int numCascade = -1)
    {
        foreach (var renderable in Renderables)
        {
            renderable.Render(_camera, new Lighting(_lightCameras, Program.shadowRenderTargets, isCreateShadowMap, numCascade));
        }
    }
}