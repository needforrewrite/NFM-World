using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Ground : Transform, IRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly Effect _material;
    private readonly int _triangleCount;

    public Ground(GraphicsDevice graphicsDevice)
    {
        // Generate a quad on World.Ground extending infinitely in X and Z
        _graphicsDevice = graphicsDevice;
        const int size = 1_000_000;
        var color = World.GroundColor.Snap(World.Snap).ToXna();
        VertexPositionColor[] data =
        [
            new(new Microsoft.Xna.Framework.Vector3(-size, World.Ground, -size), color),
            new(new Microsoft.Xna.Framework.Vector3(size, World.Ground, -size), color),
            new(new Microsoft.Xna.Framework.Vector3(-size, World.Ground, size), color),
            new(new Microsoft.Xna.Framework.Vector3(size, World.Ground, -size), color),
            new(new Microsoft.Xna.Framework.Vector3(-size, World.Ground, size), color),
            new(new Microsoft.Xna.Framework.Vector3(size, World.Ground, size), color)
        ];
        
        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Length, BufferUsage.None);
        _vertexBuffer.SetData(data);
        _triangleCount = data.Length / 3;

        _material = Program._groundShader;
    }

    public void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        if (isCreateShadowMap) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        _material.Parameters["WorldView"]?.SetValue(camera.ViewMatrix);
        _material.Parameters["WorldViewProj"]?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        
        _material.Parameters["DepthBias"]?.SetValue(0.00005f);
        _material.Parameters["FogColor"]?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.Parameters["FogDistance"]?.SetValue(World.FadeFrom);
        _material.Parameters["FogDensity"]?.SetValue(World.FogDensity / (World.FogDensity + 1f));
        if (lightCamera != null)
        {
            _material.Parameters["LightViewProj"]?.SetValue(lightCamera.ViewProjectionMatrix);
        }
        
        _material.Parameters["ShadowMap"]?.SetValue(Program.shadowRenderTarget);
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}