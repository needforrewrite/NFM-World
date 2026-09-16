using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class GroundPolys : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly int _triangleCount;
    private readonly int _vertexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public GroundPolys(GraphicsDevice graphicsDevice, Rad3dPoly[] polys)
    {
        _graphicsDevice = graphicsDevice;
        
        var data = new List<VertexPositionColor>();
        var indices = new List<uint>();
        
        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];

            var baseIndex = (uint)data.Count;
            foreach (var point in poly.Points)
            {
                var color = poly.Color;
                data.Add(new VertexPositionColor(point, color));
            }

            for (var index = 0; index < poly.Triangles.Length; index += 3)
            {
                var i0 = poly.Triangles[index];
                var i1 = poly.Triangles[index + 1];
                var i2 = poly.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }
        }

        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Count, BufferUsage.None)
        {
            Name = "Ground Polys Vertex Buffer",
            Tag = this
        };
        _vertexBuffer.SetDataEXT(data);

        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None)
        {
            Name = "Ground Polys Index Buffer",
            Tag = this
        };
        _indexBuffer.SetDataEXT(indices);
        _triangleCount = indices.Count / 3;
        _vertexCount = data.Count;
    }
    
    ~GroundPolys()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.GroundPolys), this);
    }

    public void Render(Camera cam, Lighting? lt)
    {
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        Effects.Ground.WorldView?.SetValue(cam.ViewMatrix);
        Effects.Ground.WorldViewProj?.SetValue(cam.ViewMatrix * cam.ProjectionMatrix);

        Effects.Ground.DepthBias?.SetValue(0.00005f);
        Effects.Ground.FogColor?.SetValue(World.Fog.Snap(World.Snap));
        Effects.Ground.FogDistance?.SetValue(World.FadeFrom);
        Effects.Ground.FogLogDensity?.SetValue(World.FogLogDensity);

        lt?.SetShadowMapParameters(Effects.Ground.UnderlyingEffect);

        foreach (var pass in Effects.Ground.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _vertexCount, 0, _triangleCount);
        }

        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private void ReleaseUnmanagedResources()
    {
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}