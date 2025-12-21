using System;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using HoleyDiver;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Mesh : Transform, IRenderable
{
    public Color3[] Colors;
    public Rad3dWheelDef[] Wheels;
    public Rad3dRimsDef? Rims;
    public Rad3dBoxDef[] Boxes;
    public Rad3dPoly[] Polys;
    
    // visually wasted
    public bool Wasted;

    protected readonly GraphicsDevice GraphicsDevice;

    protected Submesh?[] Submeshes;
    protected LineMesh?[]? LineMeshes;
    
    public readonly PolygonTriangulator.TriangulationResult[] Triangulation;

    // Stores "brokenness" phase for damageable meshes
    public readonly float[] Bfase;

    private readonly Mesh[] _wheels;
    private readonly CollisionDebugMesh? _collisionDebugMesh;
    internal readonly Flames Flames;
    internal readonly Dust Dust;
    internal readonly Chips Chips;
    internal readonly Sparks Sparks;

    public bool CastsShadow { get; set; }
    public bool GetsShadowed { get; set; } = true;

    public Euler TurningWheelAngle { get; set; }
    public Euler WheelAngle { get; set; }
    public int GroundAt;

    // for checkpoints
    public bool Finish = false;
    public bool Glow = false;
    
    public string FileName;
    public Mesh? ClonedMesh;
    public float? alphaOverride;

    public int MaxRadius;

    public Mesh(GraphicsDevice graphicsDevice, Rad3d rad, string fileName)
    {
        Colors = rad.Colors;
        Wheels = rad.Wheels;
        Rims = rad.Rims;
        Boxes = rad.Boxes;
        Polys = rad.Polys;
        GroundAt = rad.Wheels.FirstOrDefault().Ground;

        GraphicsDevice = graphicsDevice;

        Triangulation = Array.ConvertAll(Polys, poly => MeshHelpers.TriangulateIfNeeded(poly.Points));
        BuildMesh(graphicsDevice);

        CastsShadow = rad.CastsShadow;
        
        Bfase = new float[Polys.Length];

        _wheels = Array.ConvertAll(Wheels, wheel => new WheelMeshBuilder(wheel, Rims).BuildMesh(graphicsDevice, this));
        _collisionDebugMesh = rad.Boxes.Length > 0 ? new CollisionDebugMesh(rad.Boxes) : null;
        Flames = new Flames(this, graphicsDevice);
        Dust = new Dust(this, graphicsDevice);
        Chips = new Chips(this, graphicsDevice);
        Sparks = new Sparks(this, graphicsDevice);

        FileName = fileName;
        MaxRadius = rad.MaxRadius;
    }

    public Mesh(Mesh baseMesh, Vector3 position, Euler rotation)
    {
        Colors = baseMesh.Colors;
        Wheels = baseMesh.Wheels;
        Rims = baseMesh.Rims;
        Boxes = baseMesh.Boxes;
        // make a copy of points for damageable meshes
        Polys = Array.ConvertAll(baseMesh.Polys, poly => poly with { Points = [..poly.Points] });
        GraphicsDevice = baseMesh.GraphicsDevice;
        GroundAt = baseMesh.GroundAt;

        Triangulation = baseMesh.Triangulation;

        BuildMesh(GraphicsDevice);
        Position = position;
        Rotation = rotation;

        CastsShadow = baseMesh.CastsShadow;
        GetsShadowed = baseMesh.GetsShadowed;
        
        Bfase = new float[Polys.Length];

        _wheels = baseMesh._wheels;
        _collisionDebugMesh = baseMesh._collisionDebugMesh;
        Flames = new Flames(this, GraphicsDevice);
        Dust = new Dust(this, GraphicsDevice);
        Chips = new Chips(this, GraphicsDevice);
        Sparks = new Sparks(this, GraphicsDevice);

        FileName = baseMesh.FileName;
        ClonedMesh = baseMesh;
        MaxRadius = baseMesh.MaxRadius;
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

            var vertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);

            vertexBuffer.SetData(data.ToArray());

            var indexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
            indexBuffer.SetData(indices.ToArray());

            Submeshes[i] = new Submesh(type, this, GraphicsDevice, vertexBuffer, indexBuffer, indices.Count / 3, data.Count);
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

    public override void GameTick()
    {
        Flames.GameTick();
        Dust.GameTick();
        Chips.GameTick();
        Sparks.GameTick();
        base.GameTick();
    }

    public virtual void Render(Camera camera, Lighting? lighting = null)
    {
        var matrixWorld = MatrixWorld;

        for (var i = 0; i < _wheels.Length; i++)
        {
            var wheel = _wheels[i];
            wheel.Parent = this;
            if (Wheels[i].Rotates == 11)
            {
                wheel.Rotation = TurningWheelAngle;
            }
            else
            {
                wheel.Rotation = WheelAngle;
            }
            wheel.alphaOverride = alphaOverride;
            wheel.Render(camera, lighting);
        }

        if (_collisionDebugMesh != null && lighting?.IsCreateShadowMap != true)
        {
            _collisionDebugMesh.Parent = this;
            if (GameSparker.devRenderTrackers)
                _collisionDebugMesh.Render(camera);
        }

        foreach (var submesh in Submeshes)
        {
            // we care about the order of drawn submeshes only if we dont have an alpha override
            if (submesh != null && (submesh.PolyType != PolyType.Glass) && alphaOverride == null)
            {
                if(submesh.PolyType != PolyType.Finish || (submesh.PolyType == PolyType.Finish && Finish))
                {
                    submesh.Render(camera, lighting, matrixWorld);   
                }
            }
        }

        if (lighting?.IsCreateShadowMap != true)
        {
            if (LineMeshes != null)
            {
                foreach (var lineMesh in LineMeshes)
                {
                    lineMesh?.Render(camera, lighting, matrixWorld);
                }
            }
        }

        if (lighting?.IsCreateShadowMap != true)
        {
            Flames.Render(camera);
            Dust.Render(camera);
            Chips.Render(camera);
            Sparks.Render(camera);
        }
        
        if(alphaOverride != null)
        {
            foreach (var submesh in Submeshes)
            {
                if(submesh != null && (submesh.PolyType != PolyType.Finish || (submesh.PolyType == PolyType.Finish && Finish)))
                {
                    submesh.Render(camera, lighting, matrixWorld);   
                }   
            }
        } else
            // Render glass (translucency) last if it is the only translucent thing
            Submeshes[(int)PolyType.Glass]?.Render(camera, lighting, matrixWorld);
    }

    public void RebuildMesh()
    {
        BuildMesh(GraphicsDevice);
    }

    public void AddDust(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround)
    {
        Dust.AddDust(wheelidx, wheelx, wheely, wheelz, scx, scz, simag, tilt, onRoof, wheelGround);
	}

    public void Chip(int polyIdx, float breakFactor)
    {
        Chips.AddChip(polyIdx, breakFactor);
    }

    public void ChipWasted()
    {
        Chips.ChipWasted();
        // breakFactor = 2.0f
        // bfase = -7
	}

    public void Spark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
    {
        Sparks.AddSpark(wheelx, wheely, wheelz, scx, scy, scz, type, wheelGround);
    }
}