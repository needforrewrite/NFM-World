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
    protected readonly GraphicsDevice GraphicsDevice;

    protected Submesh?[] Submeshes;
    protected LineMesh? LineMesh;
    
    protected readonly PolygonTriangulator.TriangulationResult[] Triangulation;

    // Stores "brokenness" phase for damageable meshes
    public readonly float[] Bfase;

    public bool CastsShadow { get; set; }
    public bool GetsShadowed { get; set; } = true;

    public Euler TurningWheelAngle { get; set; }

    public Mesh(GraphicsDevice graphicsDevice, string code) : this(graphicsDevice, RadParser.ParseRad(code))
    {
    }

    public Mesh(GraphicsDevice graphicsDevice, Rad3d rad)
    {
        Colors = rad.Colors;
        Stats = rad.Stats;
        Wheels = rad.Wheels;
        Rims = rad.Rims;
        Boxes = rad.Boxes;
        Polys = rad.Polys;

        GroundAt = rad.Wheels.FirstOrDefault().Ground;
        GraphicsDevice = graphicsDevice;

        Triangulation = Array.ConvertAll(Polys,
            poly => TriangulateIfNeeded(Array.ConvertAll(poly.Points,
                input => (System.Numerics.Vector3)input)));
        BuildMesh(graphicsDevice);

        CastsShadow = rad.CastsShadow;
        
        Bfase = new float[Polys.Length];
    }

    internal static PolygonTriangulator.TriangulationResult TriangulateIfNeeded(System.Numerics.Vector3[] verts)
    {
        if (verts.Length <= 3)
        {
            // Compute triangle normal
            var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(
                verts[1] - verts[0],
                verts[2] - verts[0]
            ));
            
            // Compute centroid
            var centroid = System.Numerics.Vector3.Zero;
            foreach (var v in verts)
            {
                centroid += v;
            }
            centroid /= verts.Length;

            return new PolygonTriangulator.TriangulationResult
            {
                PlaneNormal = normal,
                Centroid = centroid,
                Triangles = verts.Length == 3 ? [0, 1, 2] : [],
                RegionCount = 1
            };
        }
        
        return PolygonTriangulator.Triangulate(verts);
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
        GraphicsDevice = baseMesh.GraphicsDevice;

        Triangulation = baseMesh.Triangulation;

        BuildMesh(GraphicsDevice);
        Position = position;
        Rotation = rotation;

        CastsShadow = baseMesh.CastsShadow;
        GetsShadowed = baseMesh.GetsShadowed;
        
        Bfase = new float[Polys.Length];
    }

    [MemberNotNull(nameof(Submeshes))]
    private void BuildMesh(GraphicsDevice graphicsDevice)
    {
        var submeshes = new (
            List<VertexPositionNormalColorCentroid> Data,
            List<int> Indices
        )[(int)(PolyType.MaxValue + 1)];

        for (var i = 0; i < submeshes.Length; i++)
        {
            submeshes[i] = ([], []);
        }
        
        var lines = new OrderedDictionary<(Vector3 point0, Vector3 point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>(LineEqualityComparer.Instance);
        
        for (var i = 0; i < Polys.Length; i++)
        {
            var poly = Polys[i];
            var result = Triangulation[i];

            var (data, indices) = submeshes[(int)poly.PolyType];
            
            var baseIndex = data.Count;
            foreach (var point in poly.Points)
            {
                var color = poly.Color.ToXna();
                data.Add(new VertexPositionNormalColorCentroid(point.ToXna(), result.PlaneNormal.ToXna(), result.Centroid.ToXna(), color));
            }

            for (var index = 0; index < result.Triangles.Length; index += 3)
            {
                var i0 = result.Triangles[index];
                var i1 = result.Triangles[index + 1];
                var i2 = result.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }

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

        Submeshes = new Submesh[submeshes.Length];
        for (var i = 0; i < submeshes.Length; i++)
        {
            var (data, indices) = submeshes[i];
            var type = (PolyType)i;
            
            if (data.Count == 0 || indices.Count == 0) continue;

            var vertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);

            vertexBuffer.SetData(data.ToArray());

            var indexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
            indexBuffer.SetData(indices.ToArray());

            Submeshes[i] = new Submesh(type, this, GraphicsDevice, vertexBuffer, indexBuffer, indices.Count / 3);
        }

        if (lines.Count > 0)
        {
            LineMesh = CreateMeshFromLines(lines);
        }
    }

    private LineMesh CreateMeshFromLines(IReadOnlyCollection<KeyValuePair<(Vector3 Point0, Vector3 Point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>> lines)
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
	    
	    var lineVertexBuffer = new VertexBuffer(GraphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);
	    lineVertexBuffer.SetData(data.ToArray());
	    
	    var lineIndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
	    lineIndexBuffer.SetData(indices.ToArray());
	    
	    var lineTriangleCount = indices.Count / 3;
        return new LineMesh(this, GraphicsDevice, lineVertexBuffer, lineIndexBuffer, lineTriangleCount);
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

        foreach (var submesh in Submeshes)
        {
            submesh?.Render(camera, lightCamera, isCreateShadowMap, matrixWorld);
        }
        if (!isCreateShadowMap) LineMesh?.Render(camera, lightCamera, matrixWorld);
    }

    public void RebuildMesh()
    {
        BuildMesh(GraphicsDevice);
    }

    public sealed override Vector3 Position { get; set; }

    public sealed override Euler Rotation { get; set; }
}