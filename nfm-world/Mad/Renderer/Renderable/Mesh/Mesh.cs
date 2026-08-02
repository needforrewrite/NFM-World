using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using HoleyDiver;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class Mesh : IDisposable
{
    public Rad3dPoly[] OriginalPolys;
    public Rad3dPoly[] Polys;

    public readonly GraphicsDevice GraphicsDevice;

    protected Submesh?[] Submeshes;
    protected LineMesh?[]? LineMeshes;
    
    public int GroundAt;
    
    public string FileName;
    public Mesh? ClonedMesh;

    public int MaxRadius;

    public bool CastsShadow;

    public bool Expand;
    public float Darken = 1.0f;

    public byte PolyFixState = 0;

    public Mesh(GraphicsDevice graphicsDevice, Rad3d rad)
    {
        // make a copy of points for damageable meshes
        OriginalPolys = rad.Polys;
        Polys = Array.ConvertAll(rad.Polys, static poly => poly.SafeClone());
        GroundAt = rad.Wheels.FirstOrDefault().Ground;

        GraphicsDevice = graphicsDevice;

        BuildMesh(graphicsDevice);

        FileName = rad.FileName;
        MaxRadius = rad.MaxRadius;
        CastsShadow = rad.CastsShadow;
    }

    public Mesh(Mesh baseMesh)
    {
        // make a copy of points for damageable meshes
        Polys = Array.ConvertAll(baseMesh.Polys, static poly => poly.SafeClone());
        GraphicsDevice = baseMesh.GraphicsDevice;
        GroundAt = baseMesh.GroundAt;

        BuildMesh(GraphicsDevice);

        FileName = baseMesh.FileName;
        ClonedMesh = baseMesh;
        MaxRadius = baseMesh.MaxRadius;
        CastsShadow = baseMesh.CastsShadow;
    }

    [MemberNotNull(nameof(Submeshes), nameof(LineMeshes))]
    private void BuildMesh(GraphicsDevice graphicsDevice)
    {
        if (Submeshes != null)
        {
            foreach (var submesh in Submeshes)
            {
                submesh?.Dispose();
            }
        }
        
        if (LineMeshes != null)
        {
            foreach (var lineMesh in LineMeshes)
            {
                lineMesh?.Dispose();
            }
        }
        
        var submeshes = new (
            List<VertexPositionNormalColorCentroid> Data,
            List<uint> Indices
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

            var (data, indices) = submeshes[(int)poly.PolyType];
            
            var baseIndex = (uint)data.Count;
            float decalOffset = poly.DecalOffset; // Use the decal offset value from polygon
            foreach (var point in poly.Points)
            {
                var color = poly.Color;
                data.Add(new VertexPositionNormalColorCentroid(point, poly.Normal, poly.Centroid, color, decalOffset));
            }

            for (var index = 0; index < poly.Triangles.Length; index += 3)
            {
                var i0 = poly.Triangles[index];
                var i1 = poly.Triangles[index + 1];
                var i2 = poly.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }

            if (poly.LineType != null)
            {
                for (var j = 0; j < poly.Points.Length; j++)
                {
                    var p0 = poly.Points[j];
                    var p1 = poly.Points[(j + 1) % poly.Points.Length];
                    lines[(int)poly.LineType].TryAdd((p0, p1), (poly, poly.Centroid, poly.Normal));
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
        /// <inheritdoc cref="P:IVertexType.VertexDeclaration" />
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

    public void SubmitRenderables(RenderQueue queue, Lighting? lighting, bool finish, BoundingSphere boundingSphere, RenderBucket renderBucket, Matrix matrixWorld, bool getsShadowed = false, float alphaOverride = 1.0f, bool isFullbright = false, bool glow = false)
    {
        var instanceData = new InstanceData(matrixWorld, getsShadowed, alphaOverride, isFullbright, glow);

        foreach (var submesh in Submeshes)
        {
            // we care about the order of drawn submeshes only if we dont have an alpha override
            if (submesh != null && (submesh.PolyType != PolyType.Finish || finish))
            {
                queue.AddInstanced(
                    submesh,
                    instanceData,
                    SortKey.Create(renderBucket, (ushort)(alphaOverride < 1f || submesh.PolyType == PolyType.Glass ? 1 : 0)),
                    boundingSphere);
            }
        }

        // HideOutlines is handled on the CPU by not submitting line meshes. Other distance
        // behavior remains in the shader because it depends on each line's centroid.
        if (lighting?.IsCreateShadowMap != true &&
            World.DistantOutlineBehavior != DistantOutlineBehavior.HideOutlines &&
            LineMeshes != null)
        {
            foreach (var lineMesh in LineMeshes)
            {
                if (lineMesh != null)
                {
                    queue.AddInstanced(
                        lineMesh,
                        instanceData,
                        SortKey.Create(renderBucket, (ushort)(alphaOverride < 1f ? 1 : 0)),
                        boundingSphere);
                }
            }
        }
    }

    private void ReleaseUnmanagedResources()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            foreach (var submesh in Submeshes)
            {
                submesh?.Dispose();
            }
        
            if (LineMeshes != null)
            {
                foreach (var lineMesh in LineMeshes)
                {
                    lineMesh?.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
