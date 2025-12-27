using HoleyDiver;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Mountains : Transform, IImmediateRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly Effect _material;
    private readonly int _triangleCount;
    private readonly int _vertexCount;

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
        
        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Count, BufferUsage.None);
        _vertexBuffer.SetDataEXT(data);
        
        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        _indexBuffer.SetDataEXT(indices);
        _triangleCount = indices.Count / 3;
        _vertexCount = data.Count;

        _material = Program._mountainsShader;
    }

    public void Render(Camera camera, Lighting? lighting = null)
    {
        if (lighting?.IsCreateShadowMap == true) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
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
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _vertexCount, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}