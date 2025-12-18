using HoleyDiver;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class GroundPolys : Transform, IRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly Effect _material;
    private readonly int _triangleCount;

    public GroundPolys(GraphicsDevice graphicsDevice, Rad3dPoly[] polys)
    {
        _graphicsDevice = graphicsDevice;
        
        var triangulation = Array.ConvertAll(polys,
            poly => MeshHelpers.TriangulateIfNeeded(Array.ConvertAll(poly.Points,
                input => (System.Numerics.Vector3)input)));

        var data = new List<VertexPositionColor>();
        var indices = new List<int>();
        
        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];
            var result = triangulation[i];

            var baseIndex = data.Count;
            foreach (var point in poly.Points)
            {
                var color = poly.Color.ToXna();
                data.Add(new VertexPositionColor(point.ToXna(), color));
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
        _vertexBuffer.SetData(data.ToArray());
        
        _indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        _indexBuffer.SetData(indices.ToArray());
        _triangleCount = indices.Count / 3;

        _material = Program._groundShader;
    }

    public void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        if (isCreateShadowMap) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
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
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}