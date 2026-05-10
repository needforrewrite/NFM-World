using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class Mountains : Transform, IImmediateRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly int _triangleCount;
    private readonly int _vertexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Mountains(GraphicsDevice graphicsDevice, Rad3dPoly[] polys)
    {
        _graphicsDevice = graphicsDevice;
        
        var triangulation = Array.ConvertAll(polys,
            poly => MeshHelpers.TriangulateIfNeeded(poly.Points));

        var data = new List<VertexPositionColor>();
        var indices = new List<int>();
        
        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];
            var result = triangulation[i];

            var baseIndex = data.Count;
            foreach (var point in poly.Points)
            {
                var color = poly.Color;
                data.Add(new VertexPositionColor(point, color));
            }

            for (var index = 0; index < result.Triangles.Length; index += 3)
            {
                var i0 = result.Triangles[index];
                var i1 = result.Triangles[index + 1];
                var i2 = result.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }
        }

        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Count, BufferUsage.None)
        {
            Name = "Mountains Vertex Buffer",
            Tag = this
        };
        _vertexBuffer.SetDataEXT(data);

        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None)
        {
            Name = "Mountains Index Buffer",
            Tag = this
        };
        _indexBuffer.SetDataEXT(indices);
        _triangleCount = indices.Count / 3;
        _vertexCount = data.Count;
    }

    ~Mountains()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public void Render(Camera camera, Lighting? lighting = null)
    {
        if (lighting?.IsCreateShadowMap == true) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        Effects.Mountains.Parameters["WorldView"]?.SetValue(camera.ViewMatrix);
        Effects.Mountains.Parameters["WorldViewProj"]?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        
        Effects.Mountains.Parameters["DepthBias"]?.SetValue(0.00005f);
        Effects.Mountains.Parameters["FogColor"]?.SetValue((Vector3)World.Fog.Snap(World.Snap));
        Effects.Mountains.Parameters["FogDistance"]?.SetValue(World.FadeFrom);
        Effects.Mountains.Parameters["FogDensity"]?.SetValue(World.FogDensity / (World.FogDensity + 1f));

        lighting?.SetShadowMapParameters(Effects.Mountains);
        foreach (var pass in Effects.Mountains.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _vertexCount, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}