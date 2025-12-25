using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Submesh : IInstancedRenderElement
{
    private readonly PolyEffect _material = new(Program._polyShader);
    public readonly PolyType PolyType;
    
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;

    private readonly int _vertexCount;
    private readonly int _triangleCount;
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;
    
    public bool Expand = false;
    public float Darken = 1.0f;

    public Submesh(
        PolyType polyType,
        Mesh supermesh,
        GraphicsDevice graphicsDevice,
        Mesh.VertexPositionNormalColorCentroid[] vertices,
        int[] indices)
    {
        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        PolyType = polyType;
        _vertexBuffer = new VertexBuffer(graphicsDevice, Mesh.VertexPositionNormalColorCentroid.VertexDeclaration, vertices.Length, BufferUsage.None);
        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.None);
        _vertexCount = vertices.Length;
        _triangleCount = indices.Length / 3;
        
        _vertexBuffer.SetData(vertices);
        _indexBuffer.SetData(indices);
    }

    public void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer)
    {
        _graphicsDevice.SetVertexBuffers(_vertexBuffer, new VertexBufferBinding(instanceBuffer, 0, 1));
        _graphicsDevice.Indices = _indexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.SnapColor?.SetValue(World.Snap.ToVector3());
        _material.IsFullbright?.SetValue((PolyType is PolyType.BrakeLight or PolyType.Light or PolyType.ReverseLight && World.LightsOn));
        _material.UseBaseColor?.SetValue(PolyType is PolyType.Glass);
        _material.BaseColor?.SetValue(World.Sky.ToVector3());
        _material.LightDirection?.SetValue(World.LightDirection);
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1f));
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.Alpha?.SetValue(PolyType is PolyType.Glass ? 0.7f : 1f);

        _graphicsDevice.BlendState = BlendState.NonPremultiplied;

        if (lighting?.IsCreateShadowMap == true)
        {
            _material.View?.SetValue(lighting.CascadeLightCamera.ViewMatrix);
            _material.Projection?.SetValue(lighting.CascadeLightCamera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(lighting.CascadeLightCamera.Position);
        }
        else
        {
            _material.View?.SetValue(camera.ViewMatrix);
            _material.Projection?.SetValue(camera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(camera.Position);
        }

        _material.CurrentTechnique = lighting?.IsCreateShadowMap == true ? _material.Techniques["CreateShadowMap"] : _material.Techniques["Basic"];
        
        lighting?.SetShadowMapParameters(_material.UnderlyingEffect);

        _material.Expand?.SetValue(Expand);
        _material.Darken?.SetValue(Darken);
        _material.RandomFloat?.SetValue(URandom.Single());
        
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _vertexCount, 0, _triangleCount);
        }
        
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
    }
}