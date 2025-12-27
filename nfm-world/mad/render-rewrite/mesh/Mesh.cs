using System;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using HoleyDiver;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Mesh
{
    public Rad3dPoly[] Polys;

    public readonly GraphicsDevice GraphicsDevice;

    protected Submesh?[] Submeshes;
    protected LineMesh?[]? LineMeshes;
    
    public readonly PolygonTriangulator.TriangulationResult[] Triangulation;

    public int GroundAt;
    
    public string FileName;
    public Mesh? ClonedMesh;

    public int MaxRadius;

    public bool CastsShadow;

    public Mesh(GraphicsDevice graphicsDevice, Rad3d rad, string fileName)
    {
        Polys = rad.Polys;
        GroundAt = rad.Wheels.FirstOrDefault().Ground;

        GraphicsDevice = graphicsDevice;

        Triangulation = Array.ConvertAll(Polys, poly => MeshHelpers.TriangulateIfNeeded(poly.Points));
        BuildMesh(graphicsDevice);

        FileName = fileName;
        MaxRadius = rad.MaxRadius;
        CastsShadow = rad.CastsShadow;
    }

    public Mesh(Mesh baseMesh)
    {
        // make a copy of points for damageable meshes
        Polys = Array.ConvertAll(baseMesh.Polys, poly => poly with { Points = [..poly.Points] });
        GraphicsDevice = baseMesh.GraphicsDevice;
        GroundAt = baseMesh.GroundAt;

        Triangulation = baseMesh.Triangulation;

        BuildMesh(GraphicsDevice);

        FileName = baseMesh.FileName;
        ClonedMesh = baseMesh;
        MaxRadius = baseMesh.MaxRadius;
        CastsShadow = baseMesh.CastsShadow;
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
        
        var lines = new OrderedDictionary<
            (Vector3 point0, Vector3 point1),
            (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)
        >[(int)(LineType.MaxValue + 1)];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = new OrderedDictionary<
                (Vector3 point0, Vector3 point1),
                (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)
            >(LineEqualityComparer.Instance);
        }
        
        for (var i = 0; i < Polys.Length; i++)
        {
            var poly = Polys[i];
            var result = Triangulation[i];

            var (data, indices) = submeshes[(int)poly.PolyType];
            
            var baseIndex = data.Count;
            float decalOffset = poly.DecalOffset; // Use the decal offset value from polygon
            foreach (var point in poly.Points)
            {
                var color = poly.Color;
                data.Add(new VertexPositionNormalColorCentroid(point, result.PlaneNormal, result.Centroid, color, decalOffset));
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
                for (var j = 0; j < poly.Points.Length; j++)
                {
                    var p0 = poly.Points[j];
                    var p1 = poly.Points[(j + 1) % poly.Points.Length];
                    lines[(int)poly.LineType].TryAdd((p0, p1), (poly, result.Centroid, result.PlaneNormal));
                }
            }
        }

        Submeshes = new Submesh[submeshes.Length];
        for (var i = 0; i < submeshes.Length; i++)
        {
            var (data, indices) = submeshes[i];
            var type = (PolyType)i;
            
            if (data.Count == 0 || indices.Count == 0) continue;

            Submeshes[i] = new Submesh(type, this, GraphicsDevice, CollectionsMarshal.AsSpan(data), CollectionsMarshal.AsSpan(indices));
        }

        LineMeshes = new LineMesh[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var lineDict = lines[i];
            if (lineDict.Count == 0) continue;
            LineMeshes[i] = new LineMesh(this, GraphicsDevice, lineDict, (LineType)i);
        }
    }

    /// <summary>
    /// Equality comparer that considers two lines equal if they have the same endpoints, regardless of order.
    /// </summary>
    private class LineEqualityComparer : IEqualityComparer<(Vector3 Point0, Vector3 Point1)>
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
        Vector3 Position,
        Vector3 Normal,
        Vector3 Centroid,
        Color Color,
        float DecalOffset)
    {
        /// <inheritdoc cref="P:Microsoft.Xna.Framework.Graphics.IVertexType.VertexDeclaration" />
        public static readonly VertexDeclaration VertexDeclaration = new(
	        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
	        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
	        new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Position, 1),
	        new VertexElement(36, VertexElementFormat.Color, VertexElementUsage.Color, 0),
	        new VertexElement(40, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0)
	    );
    }

    public virtual void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer)
    {
    }

    public void RebuildMesh()
    {
        BuildMesh(GraphicsDevice);
    }

    public IEnumerable<(IInstancedRenderElement Element, int RenderOrder)> GetRenderables(Lighting? lighting, bool finish)
    {
        foreach (var submesh in Submeshes)
        {
            // we care about the order of drawn submeshes only if we dont have an alpha override
            if (submesh != null &&
                submesh.PolyType != PolyType.Glass &&
                (submesh.PolyType != PolyType.Finish || finish))
            {
                yield return (submesh, 0);
            }
        }

        if (lighting?.IsCreateShadowMap != true)
        {
            if (LineMeshes != null)
            {
                foreach (var lineMesh in LineMeshes)
                {
                    if (lineMesh != null)
                    {
                        yield return (lineMesh, 0);
                    }
                }
            }
        }
        
        // Render glass (translucency) last if it is the only translucent thing
        if (Submeshes[(int)PolyType.Glass] is {} glassSubmesh)
        {
            yield return (glassSubmesh, 1);
        }
    }
}