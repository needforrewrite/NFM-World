using System;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using HoleyDiver;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Stride.Core.Mathematics.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector3 = Stride.Core.Mathematics.Vector3;
using Stride.Core.Mathematics;
using Vector2 = Stride.Core.Mathematics.Vector2;

namespace NFMWorld.Mad;

public class Mesh : Transform
{
    public Color3[] Colors;
    public CarStats Stats;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;
    public Rad3dBoxDef[] Boxes;
    public Rad3dPoly[] Polys;
    
    // visually wasted
    public bool Wasted;

    public int GroundAt;
    private readonly GraphicsDevice _graphicsDevice;

    private VertexBuffer _vertexBuffer;
    private IndexBuffer _indexBuffer;
    private PolyEffect _material;
    private int _triangleCount;

    private VertexBuffer? _lineVertexBuffer;
    private IndexBuffer? _lineIndexBuffer;
    private int? _lineTriangleCount;
    private readonly PolygonTriangulator.TriangulationResult[] _triangulation;

    // Stores "brokenness" phase for damageable meshes
    public readonly float[] bfase;

    public bool CastsShadow { get; set; }
    public bool GetsShadowed { get; set; } = true;

    public Euler TurningWheelAngle { get; set; }

    public Mesh(GraphicsDevice graphicsDevice, string code)
    {
        var rad = RadParser.ParseRad(code);
        Colors = rad.Colors;
        Stats = rad.Stats;
        Wheels = rad.Wheels;
        Rims = rad.Rims;
        Boxes = rad.Boxes;
        Polys = rad.Polys;
        _material = new PolyEffect(Program._polyShader);

        GroundAt = rad.Wheels.FirstOrDefault().Ground;
        _graphicsDevice = graphicsDevice;

        _triangulation = Array.ConvertAll(Polys,
            poly => PolygonTriangulator.Triangulate(Array.ConvertAll(poly.Points,
                input => (System.Numerics.Vector3)input)));
        BuildMesh(graphicsDevice);

        CastsShadow = rad.CastsShadow;
        
        bfase = new float[Polys.Length];
    }

    public Mesh(Mesh baseMesh, Vector3 position, Euler rotation)
    {
        Colors = baseMesh.Colors;
        Stats = baseMesh.Stats;
        Wheels = baseMesh.Wheels;
        Rims = baseMesh.Rims;
        Boxes = baseMesh.Boxes;
        // make a copy of points for damageable meshes
        Polys = Array.ConvertAll(baseMesh.Polys, poly => poly with { Points = [..poly.Points] });
        GroundAt = baseMesh.GroundAt;
        _graphicsDevice = baseMesh._graphicsDevice;
        _material = baseMesh._material;

        _triangulation = baseMesh._triangulation;

        BuildMesh(_graphicsDevice);
        Position = position;
        Rotation = rotation;

        CastsShadow = baseMesh.CastsShadow;
        GetsShadowed = baseMesh.GetsShadowed;
        
        bfase = new float[Polys.Length];
    }

    [MemberNotNull(nameof(_vertexBuffer), nameof(_indexBuffer))]
    private void BuildMesh(GraphicsDevice graphicsDevice)
    {
        var data = new List<VertexPositionNormalColorCentroid>();
        var indices = new List<int>();
        
        var lines = new OrderedDictionary<(Vector3 point0, Vector3 point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>(LineEqualityComparer.Instance);
        
        _triangleCount = 0;
        for (var i = 0; i < Polys.Length; i++)
        {
            var poly = Polys[i];
            var result = _triangulation[i];
            var baseIndex = data.Count;
            foreach (var point in poly.Points)
            {
                data.Add(new VertexPositionNormalColorCentroid(point.ToXna(), result.PlaneNormal.ToXna(),
                    result.Centroid.ToXna(), poly.Color.ToXna()));
            }

            for (var index = 0; index < result.Triangles.Length; index += 3)
            {
                var i0 = result.Triangles[index];
                var i1 = result.Triangles[index + 1];
                var i2 = result.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }

            _triangleCount += result.Triangles.Length;
            if (poly.LineType != null)
            {
                for (var j = 0; j < poly.Points.Length; ++j)
                {
                    var p0 = poly.Points[j];
                    var p1 = poly.Points[(j + 1) % poly.Points.Length];
                    lines.TryAdd((p0, p1), (poly, result.Centroid, result.PlaneNormal));
                }
            }
        }

        _triangleCount /= 3;

        var vertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);

        vertexBuffer.SetData(data.ToArray());

        _vertexBuffer = vertexBuffer;

        _indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        _indexBuffer.SetData(indices.ToArray());

        if (lines.Count > 0)
        {
            CreateMeshFromLines(lines);
        }
    }

    private void CreateMeshFromLines(IReadOnlyCollection<KeyValuePair<(Vector3 Point0, Vector3 Point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>> lines)
    {
	    var data = new List<VertexPositionNormalColorCentroid>(8 * lines.Count);
	    var indices = new List<int>(12 * 3 * lines.Count);

        const float halfThickness = 1f;
	    foreach (var line in lines)
	    {
            // Create two quads for each line segment to give it some thickness
            var p0 = line.Key.Point0;
            var p1 = line.Key.Point1;
            var poly = line.Value.Poly;
            var centroid = line.Value.Centroid;
            var normal = line.Value.Normal;
            var color = poly.LineType == LineType.Colored ? (poly.Color - new Color3(10, 10, 10)).ToXna() : Microsoft.Xna.Framework.Color.Black;            
            
            var lineDir = Vector3.Normalize(p1 - p0);
            var up = Vector3.UnitY;
            if (Vector3.Dot(lineDir, up) > 0.99f)
            {
                up = Vector3.UnitX; // Avoid degenerate case
            }

            var right = Vector3.Normalize(Vector3.Cross(lineDir, up)) * halfThickness; // Line half-thickness
            up = Vector3.Normalize(Vector3.Cross(right, lineDir)) * halfThickness; // Recalculate up to ensure orthogonality
            var v0 = p0 - right - up;
            var v1 = p0 + right - up;
            var v2 = p0 + right + up;
            var v3 = p0 - right + up;
            var v4 = p1 - right - up;
            var v5 = p1 + right - up;
            var v6 = p1 + right + up;
            var v7 = p1 - right + up;
            var baseIndex = data.Count;
            data.Add(new VertexPositionNormalColorCentroid(v0.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v1.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v2.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v3.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v4.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v5.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v6.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            data.Add(new VertexPositionNormalColorCentroid(v7.ToXna(), normal.ToXna(), centroid.ToXna(), color));
            // Two triangles per quad face
            indices.AddRange(
                baseIndex, baseIndex + 1, baseIndex + 2,
                baseIndex, baseIndex + 2, baseIndex + 3,
                baseIndex + 4, baseIndex + 6, baseIndex + 5,
                baseIndex + 4, baseIndex + 7, baseIndex + 6,
                baseIndex + 3, baseIndex + 2, baseIndex + 6,
                baseIndex + 3, baseIndex + 6, baseIndex + 7,
                baseIndex, baseIndex + 5, baseIndex + 1,
                baseIndex, baseIndex + 4, baseIndex + 5,
                baseIndex + 1, baseIndex + 5, baseIndex + 6,
                baseIndex + 1, baseIndex + 6, baseIndex + 2,
                baseIndex + 4, baseIndex + 0, baseIndex + 3,
                baseIndex + 4, baseIndex + 3, baseIndex + 7
            );
        }
	    
	    _lineVertexBuffer = new VertexBuffer(_graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);
	    _lineVertexBuffer.SetData(data.ToArray());
	    
	    _lineIndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
	    _lineIndexBuffer.SetData(indices.ToArray());
	    
	    _lineTriangleCount = indices.Count / 3;
    }

    /// <summary>
    /// Equality comparer that considers two lines equal if they have the same endpoints, regardless of order.
    /// </summary>
    private struct LineEqualityComparer : IEqualityComparer<(Vector3 Point0, Vector3 Point1)>
    {
        public static LineEqualityComparer Instance { get; } = new();

        public bool Equals((Vector3 Point0, Vector3 Point1) x, (Vector3 Point0, Vector3 Point1) y)
        {
            return (x.Point0 == y.Point0 && x.Point1 == y.Point1) ||
                   (x.Point0 == y.Point1 && x.Point1 == y.Point0);
        }

        public int GetHashCode((Vector3 Point0, Vector3 Point1) obj)
        {
            return obj.Point0.GetHashCode() ^ obj.Point1.GetHashCode();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct VertexPositionNormalColorCentroid(
        Microsoft.Xna.Framework.Vector3 Position,
        Microsoft.Xna.Framework.Vector3 Normal,
        Microsoft.Xna.Framework.Vector3 Centroid,
        Microsoft.Xna.Framework.Color Color)
    {
        /// <inheritdoc cref="P:Microsoft.Xna.Framework.Graphics.IVertexType.VertexDeclaration" />
        public static readonly VertexDeclaration VertexDeclaration = new(
	        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
	        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
	        new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Position, 1),
	        new VertexElement(36, VertexElementFormat.Color, VertexElementUsage.Color, 0)
	    );
    }

    public virtual void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        var matrixWorld = Matrix.CreateFromEuler(Rotation) * Matrix.CreateTranslation(Position.ToXna());

        RenderPolygons(camera, lightCamera, isCreateShadowMap, matrixWorld);
        RenderLines(camera, lightCamera, isCreateShadowMap, matrixWorld);
    }

    private void RenderPolygons(Camera camera, Camera? lightCamera, bool isCreateShadowMap, Matrix matrixWorld)
    {
        if (isCreateShadowMap && !(CastsShadow || Position.Y < World.Ground)) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToXnaVector3());
        _material.IsFullbright?.SetValue(false);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 0, 0));
        _material.LightDirection?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 1, 0));
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(0.857f);
        _material.EnvironmentLight?.SetValue(new Microsoft.Xna.Framework.Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(GetsShadowed);

        if (isCreateShadowMap)
        {
            _material.View?.SetValue(lightCamera!.ViewMatrix);
            _material.Projection?.SetValue(lightCamera!.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * lightCamera!.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * lightCamera!.ViewMatrix * lightCamera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(lightCamera!.Position.ToXna());
        }
        else
        {
            _material.View?.SetValue(camera.ViewMatrix);
            _material.Projection?.SetValue(camera.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(camera.Position.ToXna());
        }

        if (lightCamera != null)
        {
            _material.LightViewProj?.SetValue(lightCamera.ViewProjectionMatrix);
        }

        _material.CurrentTechnique = isCreateShadowMap ? _material.Techniques["CreateShadowMap"] : _material.Techniques["Basic"];
        if (!isCreateShadowMap)
        {
            _material.ShadowMap?.SetValue(Program.shadowRenderTarget);
        }
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triangleCount);
        }
        
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
    }

    private void RenderLines(Camera camera, Camera? lightCamera, bool isCreateShadowMap, Matrix matrixWorld)
    {
        if (isCreateShadowMap) return; // no lines in shadow map
        if (_lineVertexBuffer == null) return; // no lines to render

        _graphicsDevice.SetVertexBuffer(_lineVertexBuffer);
        _graphicsDevice.Indices = _lineIndexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToXnaVector3());
        _material.IsFullbright?.SetValue(false);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 0, 0));
        _material.LightDirection?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 1, 0));
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(0.857f);
        _material.EnvironmentLight?.SetValue(new Microsoft.Xna.Framework.Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(GetsShadowed);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
        _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position.ToXna());

        _material.CurrentTechnique = _material.Techniques["Basic"];
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _lineTriangleCount!.Value);
        }
    }

    public void RebuildMesh()
    {
        BuildMesh(_graphicsDevice);
    }

    public sealed override Vector3 Position { get; set; }

    public sealed override Euler Rotation { get; set; }
}