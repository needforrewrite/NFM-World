using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class LineMesh : IInstancedRenderElement, IDisposable
{
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _lineVertexBuffer;
    private readonly IndexBuffer _lineIndexBuffer;
    private readonly int _lineTriangleCount;
    private readonly LineType _lineType;
    private readonly int _lineVertexCount;

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

        var lineVertexBuffer = new VertexBuffer(graphicsDevice, LineMeshVertexAttribute.VertexDeclaration, data.Count, BufferUsage.None)
        {
            Name = "Line Mesh Vertex Buffer",
            Tag = this
        };
        lineVertexBuffer.SetDataEXT(data);

        var lineIndexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None)
        {
            Name = "Line Mesh Index Buffer",
            Tag = this
        };
        lineIndexBuffer.SetDataEXT(indices);

        var lineVertexCount = data.Count;
        var lineTriangleCount = indices.Count / 3;

        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        _lineVertexBuffer = lineVertexBuffer;
        _lineIndexBuffer = lineIndexBuffer;
        _lineTriangleCount = lineTriangleCount;
        _lineVertexCount = lineVertexCount;
    }

    ~LineMesh()
    {
        Dispose(false);
    }

    public void Render(Camera camera, Lighting? lighting, VertexBuffer instanceBuffer, int instanceCount)
    {
        if (World.DistantOutlineBehavior == DistantOutlineBehavior.HideOutlines)
            return;

        _graphicsDevice.SetVertexBuffers(_lineVertexBuffer, new VertexBufferBinding(instanceBuffer, 0, 1));
        _graphicsDevice.Indices = _lineIndexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        // If a parameter is null that means the HLSL compiler optimized it out.
        Effects.Line.SnapColor?.SetValue(World.Snap);
        Effects.Line.IsFullbright?.SetValue(false);
        Effects.Line.UseBaseColor?.SetValue(false);
        Effects.Line.BaseColor?.SetValue(new Vector3(0, 0, 0));
        Effects.Line.ChargedBlinkAmount?.SetValue(_lineType is LineType.Charged && World.ChargedPolyBlink ? World.ChargeAmount : 0.0f);
        Effects.Line.HalfThickness?.SetValue(World.OutlineThickness);

        // Line meshes are batched, so cutoff and falloff are evaluated per-line in the shader.
        LineEffectDistantOutlineSettings.Apply(World.DistantOutlineBehavior);
        Effects.Line.OutlineClassicCutoffDistance?.SetValue(World.OutlineClassicCutoffDistance);
        Effects.Line.OutlineFalloffStartDistance?.SetValue(World.OutlineFalloffStartDistance);
        var (cutoffDistance, linearFadeStartDistance, linearFadeStartThickness, inverseLinearFadeLength) =
            GetOutlineFalloffCutoffParameters();
        Effects.Line.OutlineFalloffCutoffDistance?.SetValue(cutoffDistance);
        Effects.Line.OutlineFalloffLinearFadeStartDistance?.SetValue(linearFadeStartDistance);
        Effects.Line.OutlineFalloffLinearFadeStartThickness?.SetValue(linearFadeStartThickness);
        Effects.Line.OutlineFalloffInverseLinearFadeLength?.SetValue(inverseLinearFadeLength);

        Effects.Line.LightDirection?.SetValue(World.LightDirection);
        Effects.Line.FogColor?.SetValue(World.Fog.Snap(World.Snap));
        Effects.Line.FogDistance?.SetValue(World.FadeFrom);
        Effects.Line.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1));
        Effects.Line.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        Effects.Line.DepthBias?.SetValue(0.00005f);

        Effects.Line.View?.SetValue(camera.ViewMatrix);
        Effects.Line.Projection?.SetValue(camera.ProjectionMatrix);
        Effects.Line.ViewProj?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        Effects.Line.CameraPosition?.SetValue(camera.Position);

        Effects.Line.CurrentTechnique = Effects.Line.Techniques["Basic"];

        Effects.Line.Expand?.SetValue(_supermesh.Expand);
        Effects.Line.Darken?.SetValue(_supermesh.Darken);
        Effects.Line.RandomFloat?.SetValue(URandom.Single());
        Effects.Line.Alpha?.SetValue(1.0f);

        Effects.Line.Resolution?.SetValue(new Vector2(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height));

        if (_supermesh.PolyFixState == 2)
        {
            Effects.Line.UseBaseColor?.SetValue(true);
            Effects.Line.BaseColor?.SetValue(new Vector3(0, 0, 0));
            Effects.Line.IsFullbright?.SetValue(true);
        }
        else if (_supermesh.PolyFixState == 1)
        {
            const short r = 0;
            short g = (short) (223F + 223F * (World.Snap[1] / 100F));
            if (g > 255) g = 255;
            if (g < 0) g = 0;
            short b = (short) (255F + 255F * (World.Snap[2] / 100F));
            if (b > 255) b = 255;
            if (b < 0) b = 0;
            
            Effects.Line.UseBaseColor?.SetValue(true);
            Effects.Line.BaseColor?.SetValue(new Color3(r, g, b));
            Effects.Line.IsFullbright?.SetValue(true);
        }
        else if (_supermesh.PolyFixState == 3)
        {
            const short r = 0;
            short g = (short) (255.0F + 255.0F * (World.Snap[1] / 100.0F));
            if (g > 255) g = 255;
            if (g < 0) g = 0;
            short b = (short) (223.0F + 223.0F * (World.Snap[2] / 100.0F));
            if (b > 255) b = 255;
            if (b < 0) b = 0;
            
            Effects.Line.UseBaseColor?.SetValue(true);
            Effects.Line.BaseColor?.SetValue(new Color3(r, g, b));
            Effects.Line.IsFullbright?.SetValue(true);
        }
        else if (_supermesh.PolyFixState == 77)
        {
            Effects.Line.UseBaseColor?.SetValue(true);
            Effects.Line.BaseColor?.SetValue(new Color3(16, 198, 255));
            Effects.Line.IsFullbright?.SetValue(true);
        }

        lighting?.SetShadowMapParameters(Effects.Line.UnderlyingEffect);
        
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;

        foreach (var pass in Effects.Line.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, _lineVertexCount, 0, _lineTriangleCount, instanceCount);
        }
    }

    private static (
        float CutoffDistance,
        float LinearFadeStartDistance,
        float LinearFadeStartThickness,
        float InverseLinearFadeLength
    ) GetOutlineFalloffCutoffParameters()
    {
        const float epsilon = 0.0001f;
        var outlineThickness = MathF.Max(World.OutlineThickness, 0f);
        var falloffStartDistance = MathF.Max(World.OutlineFalloffStartDistance, epsilon);
        var minimumVisibleThickness = MathF.Max(World.OutlineMinimumVisibleThickness, epsilon);

        // Inverse-depth sizing reaches the minimum at this width-dependent depth. Replace
        // its final section with a linear fade so it reaches zero without a hard pop.
        var cutoffDistance = falloffStartDistance * outlineThickness / minimumVisibleThickness;
        var linearFadeStartDistance = MathF.Max(
            falloffStartDistance,
            cutoffDistance - MathF.Max(World.OutlineLinearFadeDistance, 0f)
        );
        var linearFadeLength = MathF.Max(cutoffDistance - linearFadeStartDistance, epsilon);
        var linearFadeStartThickness = outlineThickness *
                                       MathF.Min(1f, falloffStartDistance / linearFadeStartDistance);

        return (
            cutoffDistance,
            linearFadeStartDistance,
            linearFadeStartThickness,
            1f / linearFadeLength
        );
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct LineMeshVertexAttribute(
        Vector3 PositionA,
        Vector3 PositionB,
        float Side,
        Vector3 Normal,
        Vector3 Centroid,
        Color Color,
        float DecalOffset
    )
    {
        /// <inheritdoc cref="P:IVertexType.VertexDeclaration" />
        public static readonly VertexDeclaration VertexDeclaration = VertexPacker.Pack(
            new VertexPacker.Element(VertexElementFormat.Vector3, VertexElementUsage.Position, 0), // PositionA
            new VertexPacker.Element(VertexElementFormat.Vector3, VertexElementUsage.Position, 1), // PositionB
            new VertexPacker.Element(VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0), // Side
            new VertexPacker.Element(VertexElementFormat.Vector3, VertexElementUsage.Normal, 0), // Normal
            new VertexPacker.Element(VertexElementFormat.Vector3, VertexElementUsage.Position, 2), // Centroid
            new VertexPacker.Element(VertexElementFormat.Color, VertexElementUsage.Color, 0), // Color
            new VertexPacker.Element(VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1) // DecalOffset
        );
    }

    private void ReleaseUnmanagedResources()
    {
        _lineVertexBuffer.Dispose();
        _lineIndexBuffer.Dispose();
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

internal static class LineEffectDistantOutlineSettings
{
    public static void Apply(DistantOutlineBehavior behavior)
    {
        // Independent numeric switches keep the shader path branchless.
        Effects.Line.DistantOutlineDistanceFalloffWithCutoffMask?.SetValue(
            behavior == DistantOutlineBehavior.DistanceFalloffWithCutoff ? 1f : 0f);
        Effects.Line.DistantOutlineClassicCutoffMask?.SetValue(
            behavior == DistantOutlineBehavior.ClassicCutoff ? 1f : 0f);
        Effects.Line.DistantOutlineDistanceFalloffMask?.SetValue(
            behavior == DistantOutlineBehavior.DistanceFalloff ? 1f : 0f);
    }
}
