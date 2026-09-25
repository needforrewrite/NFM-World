using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class LineMesh : IInstancedRenderElement, IDisposable
{
    private readonly Mesh _supermesh;
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IBuffer _lineVertexBuffer;
    private readonly IBuffer _lineIndexBuffer;
    private readonly int _lineTriangleCount;
    private readonly LineType _lineType;
    private readonly int _lineVertexCount;

    public LineMesh(
        Mesh supermesh,
        IGraphicsDevice graphicsDevice,
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

        var vertexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(data));
        var lineVertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length), vertexBytes);

        // Indices are stored as List<int> (32-bit) - matches the old IndexElementSize.ThirtyTwoBits.
        var indexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(indices));
        var lineIndexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indexBytes.Length, IndexFormat.UInt32), indexBytes);

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

    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting, IBuffer instanceBuffer, int instanceCount)
    {
        if (World.DistantOutlineBehavior == DistantOutlineBehavior.HideOutlines)
            return;

        // The editor's translucent line overlays select the depth-read variant; line meshes are
        // never submitted in the shadow pass (see Mesh.SubmitRenderables), so there's no shadow arm
        // here. Each pipeline has its own parameter set - SetUniform writes into the *bound*
        // pipeline's uniform storage, so the two must always be picked together.
        var (pipeline, p) = _supermesh.OverlayMode switch
        {
            PolyOverlayMode.DepthRead => (Effects.LineDepthReadPipeline, Effects.LineDepthReadParameters),
            _ => (Effects.LinePipeline, Effects.LineParameters),
        };

        cb.SetPipeline(pipeline);
        cb.SetVertexBuffer(0, _lineVertexBuffer, LineMeshVertexAttribute.Stride);
        cb.SetVertexBuffer(1, instanceBuffer, InstanceData.Stride);
        cb.SetIndexBuffer(_lineIndexBuffer);

        // If a parameter's slot is -1 that means the HLSL compiler optimized it out.
        p.SnapColor.SetValue(cb, World.Snap);
        p.IsFullbright.SetValue(cb, false);
        p.UseBaseColor.SetValue(cb, false);
        p.BaseColor.SetValue(cb, new Vector3(0, 0, 0));
        p.ChargedBlinkAmount.SetValue(cb, _lineType is LineType.Charged && World.ChargedPolyBlink ? World.ChargeAmount : 0.0f);
        p.HalfThickness.SetValue(cb, World.OutlineThickness);

        // Line meshes are batched, so cutoff and falloff are evaluated per-line in the shader.
        LineEffectDistantOutlineSettings.Apply(cb, World.DistantOutlineBehavior);
        p.OutlineClassicCutoffDistance.SetValue(cb, World.OutlineClassicCutoffDistance);
        p.OutlineFalloffStartDistance.SetValue(cb, World.OutlineFalloffStartDistance);
        var (cutoffDistance, linearFadeStartDistance, linearFadeStartThickness, inverseLinearFadeLength) =
            GetOutlineFalloffCutoffParameters();
        p.OutlineFalloffCutoffDistance.SetValue(cb, cutoffDistance);
        p.OutlineFalloffLinearFadeStartDistance.SetValue(cb, linearFadeStartDistance);
        p.OutlineFalloffLinearFadeStartThickness.SetValue(cb, linearFadeStartThickness);
        p.OutlineFalloffInverseLinearFadeLength.SetValue(cb, inverseLinearFadeLength);

        p.LightDirection.SetValue(cb, World.LightDirection);
        p.FogColor.SetValue(cb, World.Fog.Snap(World.Snap));
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);
        p.EnvironmentLight.SetValue(cb, new Vector2(World.BlackPoint, World.WhitePoint));
        p.DepthBias.SetValue(cb, 0.00005f);

        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);
        p.ViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.CameraPosition.SetValue(cb, camera.Position);

        p.Expand.SetValue(cb, _supermesh.Expand);
        p.Darken.SetValue(cb, _supermesh.Darken);
        p.RandomFloat.SetValue(cb, URandom.Single());
        p.Alpha.SetValue(cb, 1.0f);

        p.Resolution.SetValue(cb, new Vector2(_graphicsDevice.Swapchain.Width, _graphicsDevice.Swapchain.Height));

        if (_supermesh.PolyFixState == 2)
        {
            p.UseBaseColor.SetValue(cb, true);
            p.BaseColor.SetValue(cb, new Vector3(0, 0, 0));
            p.IsFullbright.SetValue(cb, true);
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

            p.UseBaseColor.SetValue(cb, true);
            p.BaseColor.SetValue(cb, new Color3(r, g, b));
            p.IsFullbright.SetValue(cb, true);
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

            p.UseBaseColor.SetValue(cb, true);
            p.BaseColor.SetValue(cb, new Color3(r, g, b));
            p.IsFullbright.SetValue(cb, true);
        }
        else if (_supermesh.PolyFixState == 77)
        {
            p.UseBaseColor.SetValue(cb, true);
            p.BaseColor.SetValue(cb, new Color3(16, 198, 255));
            p.IsFullbright.SetValue(cb, true);
        }

        lighting?.SetShadowMapParameters(cb, Effects.LinePipeline.Reflection);

        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: _lineTriangleCount, instanceCount: instanceCount);
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
        public const int Stride = 60;

        /// <summary>
        /// Same field layout as this struct's own memory layout (<see cref="Stride"/> = 60 bytes:
        /// PositionA/B float3 pairs, Side float1, Normal float3, Centroid float3, Color as a packed
        /// byte4 despite HLSL's "float3 Color : COLOR0" - the input assembler unpacks bytes to
        /// normalized floats before the shader runs - then DecalOffset float1), expressed against
        /// the new graphics abstraction for <see cref="Graphics.PipelineDesc.VertexLayouts"/>.
        ///
        /// Written in Line.fx's <em>declaration</em> order rather than this struct's memory order,
        /// for the same reason <see cref="Mesh.VertexPositionNormalColorCentroid.VertexLayout"/>
        /// is: the GL backend numbers attribute locations by position in this list, so the entry
        /// order has to match the shader's. Line.fx declares Color (:COLOR0) before Centroid
        /// (:POSITION2) while the struct stores Centroid first, and listing them in memory order
        /// puts each on the other's location. The byte offsets below are what the vertex data
        /// actually is, so reordering them moves no data - only which location each binds to.
        /// </summary>
        public static readonly VertexLayoutDesc VertexLayout = new(
            Attributes:
            [
                new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),
                new VertexAttributeDesc("POSITION", 1, 12, VertexAttributeFormat.Float3),
                new VertexAttributeDesc("TEXCOORD", 0, 24, VertexAttributeFormat.Float1),
                new VertexAttributeDesc("NORMAL", 0, 28, VertexAttributeFormat.Float3),
                new VertexAttributeDesc("COLOR", 0, 52, VertexAttributeFormat.Byte4Normalized),
                new VertexAttributeDesc("POSITION", 2, 40, VertexAttributeFormat.Float3),
                new VertexAttributeDesc("TEXCOORD", 1, 56, VertexAttributeFormat.Float1),
            ],
            StrideInBytes: Stride);
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
    public static void Apply(ICommandBuffer cb, DistantOutlineBehavior behavior)
    {
        var p = Effects.LineParameters;

        // Independent numeric switches keep the shader path branchless.
        p.DistantOutlineDistanceFalloffWithCutoffMask.SetValue(cb,
            behavior == DistantOutlineBehavior.DistanceFalloffWithCutoff ? 1f : 0f);
        p.DistantOutlineClassicCutoffMask.SetValue(cb,
            behavior == DistantOutlineBehavior.ClassicCutoff ? 1f : 0f);
        p.DistantOutlineDistanceFalloffMask.SetValue(cb,
            behavior == DistantOutlineBehavior.DistanceFalloff ? 1f : 0f);
    }
}
