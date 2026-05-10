using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class Submesh : IInstancedRenderElement, IDisposable
{
    public readonly PolyType PolyType;
    
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;

    private readonly int _vertexCount;
    private readonly int _triangleCount;
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;

    public Submesh(
        PolyType polyType,
        Mesh supermesh,
        GraphicsDevice graphicsDevice,
        ReadOnlySpan<Mesh.VertexPositionNormalColorCentroid> vertices,
        ReadOnlySpan<int> indices)
    {
        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        PolyType = polyType;
        _vertexBuffer = new VertexBuffer(graphicsDevice, Mesh.VertexPositionNormalColorCentroid.VertexDeclaration, vertices.Length, BufferUsage.None)
        {
            Name = "Submesh Vertex Buffer",
            Tag = this
        };
        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.None)
        {
            Name = "Submesh Index Buffer",
            Tag = this
        };
        _vertexCount = vertices.Length;
        _triangleCount = indices.Length / 3;
        
        _vertexBuffer.SetDataEXT(vertices);
        _indexBuffer.SetDataEXT(indices);
    }

    ~Submesh()
    {
        Dispose(false);
    }

    public void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount)
    {
        _graphicsDevice.SetVertexBuffers(_vertexBuffer, new VertexBufferBinding(instanceBuffer, 0, 1));
        _graphicsDevice.Indices = _indexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        Effects.Poly.SnapColor?.SetValue((Vector3)World.Snap);
        Effects.Poly.IsFullbright?.SetValue((PolyType is PolyType.BrakeLight or PolyType.Light or PolyType.ReverseLight && World.LightsOn));
        Effects.Poly.UseBaseColor?.SetValue(PolyType is PolyType.Glass);
        Effects.Poly.BaseColor?.SetValue((Vector3)World.Sky);
        Effects.Poly.LightDirection?.SetValue(World.LightDirection);
        Effects.Poly.FogColor?.SetValue((Vector3)World.Fog.Snap(World.Snap));
        Effects.Poly.FogDistance?.SetValue(World.FadeFrom);
        Effects.Poly.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1f));
        Effects.Poly.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        Effects.Poly.DepthBias?.SetValue(0.00005f);
        Effects.Poly.Alpha?.SetValue(PolyType is PolyType.Glass ? 0.7f : 1f);

        _graphicsDevice.BlendState = BlendState.NonPremultiplied;

        if (lighting?.IsCreateShadowMap == true)
        {
            Effects.Poly.View?.SetValue(lighting.CascadeLightCamera.ViewMatrix);
            Effects.Poly.Projection?.SetValue(lighting.CascadeLightCamera.ProjectionMatrix);
            Effects.Poly.CameraPosition?.SetValue(lighting.CascadeLightCamera.Position);
            Effects.Poly.ViewProj?.SetValue(lighting.CascadeLightCamera.ViewMatrix * lighting.CascadeLightCamera.ProjectionMatrix);
        }
        else
        {
            Effects.Poly.View?.SetValue(camera.ViewMatrix);
            Effects.Poly.Projection?.SetValue(camera.ProjectionMatrix);
            Effects.Poly.CameraPosition?.SetValue(camera.Position);
            Effects.Poly.ViewProj?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        }

        Effects.Poly.CurrentTechnique = lighting?.IsCreateShadowMap == true ? Effects.Poly.Techniques["CreateShadowMap"] : Effects.Poly.Techniques["Basic"];
        
        lighting?.SetShadowMapParameters(Effects.Poly.UnderlyingEffect);

        Effects.Poly.Expand?.SetValue(_supermesh.Expand);
        Effects.Poly.Darken?.SetValue(_supermesh.Darken);
        Effects.Poly.RandomFloat?.SetValue(URandom.Single());
        
        foreach (var pass in Effects.Poly.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, _vertexCount, 0, _triangleCount, instanceCount);
        }
        
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
    }

    private void ReleaseUnmanagedResources()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}