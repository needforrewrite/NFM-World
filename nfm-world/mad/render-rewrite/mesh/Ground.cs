using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Ground : Transform, IImmediateRenderable
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
        var color = World.GroundColor.Snap(World.Snap);
        Span<VertexPositionColor> data =
        [
            new(new Vector3(-size, World.Ground, -size), color),
            new(new Vector3(size, World.Ground, -size), color),
            new(new Vector3(-size, World.Ground, size), color),
            new(new Vector3(size, World.Ground, -size), color),
            new(new Vector3(-size, World.Ground, size), color),
            new(new Vector3(size, World.Ground, size), color)
        ];

        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Length, BufferUsage.None)
        {
            Name = "Ground Vertex Buffer",
            Tag = this
        };
        _vertexBuffer.SetDataEXT(data);
        _triangleCount = data.Length / 3;

        _material = Program._groundShader;
    }
    
    ~Ground()
    {
        _vertexBuffer.Dispose();
    }

    public void Render(Camera camera, Lighting? lighting = null)
    {
        if (lighting?.IsCreateShadowMap == true) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        _material.Parameters["WorldView"]?.SetValue(camera.ViewMatrix);
        _material.Parameters["WorldViewProj"]?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        
        _material.Parameters["DepthBias"]?.SetValue(0.00005f);
        _material.Parameters["FogColor"]?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.Parameters["FogDistance"]?.SetValue(World.FadeFrom);
        _material.Parameters["FogDensity"]?.SetValue(World.FogDensity / (World.FogDensity + 1f));
        
        lighting?.SetShadowMapParameters(_material);
        
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}