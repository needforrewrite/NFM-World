using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class LineMesh : IInstancedRenderElement
{
    private readonly LineEffect _material = new(Program._lineShader);
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _lineVertexBuffer;
    private readonly IndexBuffer _lineIndexBuffer;
    private readonly int _lineTriangleCount;
    private readonly LineType _lineType;
    private readonly int _lineVertexCount;

    public bool Expand = false;
    public float Darken = 1.0f;

    public LineMesh(
        Mesh supermesh,
        GraphicsDevice graphicsDevice,
        IReadOnlyCollection<KeyValuePair<(Vector3 Point0, Vector3 Point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>> lines,
        LineType lineType
    )
    {
        _lineType = lineType;
        var data = new List<LineMeshVertexAttribute>(LineMeshHelpers.VerticesPerLine * lines.Count);
        var indices = new List<int>(LineMeshHelpers.IndicesPerLine * lines.Count);

        const float halfThickness = 1f;
        Span<LineMeshVertexAttribute> verts = stackalloc LineMeshVertexAttribute[LineMeshHelpers.VerticesPerLine];
        Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

        foreach (var line in lines)
        {
            // Create two quads for each line segment to give it some thickness
            var p0 = line.Key.Point0;
            var p1 = line.Key.Point1;
            var poly = line.Value.Poly;
            var centroid = line.Value.Centroid;
            var normal = line.Value.Normal;
            var color = poly.LineType switch
            {
                LineType.Colored => (poly.Color - new Color3(10, 10, 10)),
                LineType.Charged => poly.Color,
                LineType.BrightColored => poly.Color,
                _ => Color.Black
            };

            LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, normal, centroid, color, 0.0f, in verts, in inds);
            indices.AddRange(inds);
            data.AddRange(verts);
        }

        var lineVertexBuffer = new VertexBuffer(graphicsDevice,
            LineMeshVertexAttribute.VertexDeclaration, data.Count, BufferUsage.None);
        lineVertexBuffer.SetData(data.ToArray());

        var lineIndexBuffer =
            new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        lineIndexBuffer.SetData(indices.ToArray());

        var lineVertexCount = data.Count;
        var lineTriangleCount = indices.Count / 3;

        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        _lineVertexBuffer = lineVertexBuffer;
        _lineIndexBuffer = lineIndexBuffer;
        _lineTriangleCount = lineTriangleCount;
        _lineVertexCount = lineVertexCount;
    }

    public void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer)
    {
        _graphicsDevice.SetVertexBuffers(_lineVertexBuffer, new VertexBufferBinding(instanceBuffer, 0, 1));
        _graphicsDevice.Indices = _lineIndexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;

        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.SnapColor?.SetValue(World.Snap.ToVector3());
        _material.IsFullbright?.SetValue(false);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Vector3(0, 0, 0));
        _material.ChargedBlinkAmount?.SetValue(_lineType is LineType.Charged && World.ChargedPolyBlink ? World.ChargeAmount : 0.0f);
        _material.HalfThickness?.SetValue(World.OutlineThickness);

        _material.LightDirection?.SetValue(World.LightDirection);
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1));
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position);

        _material.CurrentTechnique = _material.Techniques["Basic"];

        _material.Expand?.SetValue(Expand);
        _material.Darken?.SetValue(Darken);
        _material.RandomFloat?.SetValue(URandom.Single());

        lighting?.SetShadowMapParameters(_material.UnderlyingEffect);
        
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;

        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _lineVertexCount, 0, _lineTriangleCount);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct LineMeshVertexAttribute(
        Vector3 Position,
        Vector3 Normal,
        Vector3 Centroid,
        Color Color,
        float DecalOffset,
        Vector3 Right,
        Vector3 Up
    )
    {
        /// <inheritdoc cref="P:Microsoft.Xna.Framework.Graphics.IVertexType.VertexDeclaration" />
        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Vector3, VertexElementUsage.Position, 1),
            new VertexElement(36, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(40, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(44, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(56, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 2)
        );
    }
}