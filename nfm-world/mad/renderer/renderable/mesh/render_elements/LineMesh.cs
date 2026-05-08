using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world.camera;
using nfm_world.compat;
using nfm_world.renderable.mesh.utils;
using GpuBuffer = MoonWorks.Graphics.Buffer;
using VertexElementFormat = MoonWorks.Graphics.VertexElementFormat;

namespace nfm_world.renderable.mesh.render_elements;

public class LineMesh : IInstancedRenderElement, IDisposable
{
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GpuBuffer _lineVertexBuffer;
    private readonly GpuBuffer _lineIndexBuffer;
    private readonly uint _lineIndexCount;
    private readonly LineType _lineType;

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

        var vtxCount = (uint)data.Count;
        _lineIndexCount = (uint)indices.Count;

        // Create and upload vertex buffer
        _lineVertexBuffer = GpuBuffer.Create<LineMeshVertexAttribute>(
            graphicsDevice, BufferUsageFlags.Vertex, vtxCount);
        {
            var transfer = TransferBuffer.Create<LineMeshVertexAttribute>(
                graphicsDevice, TransferBufferUsage.Upload, vtxCount);
            var span = transfer.Map<LineMeshVertexAttribute>(false);
            CollectionsMarshal.AsSpan(data).CopyTo(span);
            transfer.Unmap();

            var cmd = graphicsDevice.AcquireCommandBuffer();
            var copyPass = cmd.BeginCopyPass();
            copyPass.UploadToBuffer(
                new TransferBufferLocation(transfer, 0),
                new BufferRegion(_lineVertexBuffer, 0, vtxCount * (uint)Marshal.SizeOf<LineMeshVertexAttribute>()),
                false);
            cmd.EndCopyPass(copyPass);
            graphicsDevice.Submit(cmd);
            transfer.Dispose();
        }

        // Create and upload index buffer
        _lineIndexBuffer = GpuBuffer.Create<int>(graphicsDevice, BufferUsageFlags.Index, _lineIndexCount);
        {
            var transfer = TransferBuffer.Create<int>(
                graphicsDevice, TransferBufferUsage.Upload, _lineIndexCount);
            var span = transfer.Map<int>(false);
            CollectionsMarshal.AsSpan(indices).CopyTo(span);
            transfer.Unmap();

            var cmd = graphicsDevice.AcquireCommandBuffer();
            var copyPass = cmd.BeginCopyPass();
            copyPass.UploadToBuffer(
                new TransferBufferLocation(transfer, 0),
                new BufferRegion(_lineIndexBuffer, 0, _lineIndexCount * sizeof(int)),
                false);
            cmd.EndCopyPass(copyPass);
            graphicsDevice.Submit(cmd);
            transfer.Dispose();
        }

        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
    }

    ~LineMesh()
    {
        Dispose(false);
    }

    public void Render(Camera camera, Lighting? lighting, GpuBuffer instanceBuffer, int instanceCount)
    {
        if (lighting?.IsCreateShadowMap == true) return; // Lines don't cast shadows

        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;

        var vertUniforms = new LineVertexUniforms
        {
            View = camera.ViewMatrix,
            Projection = camera.ProjectionMatrix,
            ViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
            CameraPosition = camera.Position,
            Alpha = 1.0f,
            SnapColor = (Vector3)World.Snap,
            Darken = _supermesh.Darken,
            LightDirection = World.LightDirection,
            RandomFloat = URandom.Single(),
            EnvironmentLight = new Vector2(World.BlackPoint, World.WhitePoint),
            IsFullbright = false,
            UseBaseColor = false,
            BaseColor = Vector3.Zero,
            Expand = _supermesh.Expand,
            HalfThickness = World.OutlineThickness,
            ChargedBlinkAmount = _lineType is LineType.Charged && World.ChargedPolyBlink ? World.ChargeAmount : 0.0f,
            Fog = new FogParams
            {
                Color = (Vector3)World.Fog.Snap(World.Snap),
                Distance = World.FadeFrom,
                Density = World.FogDensity / (World.FogDensity + 1f)
            }
        };
        cmd.PushVertexUniformData(vertUniforms);

        var fragUniforms = new LineFragUniforms
        {
            Shadow = lighting?.ToShadowParams() ?? default
        };
        cmd.PushFragmentUniformData(fragUniforms);

        pass.BindGraphicsPipeline(Pipelines.Line);
        pass.BindVertexBuffers(0,
            new BufferBinding(_lineVertexBuffer, 0),
            new BufferBinding(instanceBuffer, 0));
        pass.BindIndexBuffer(new BufferBinding(_lineIndexBuffer, 0), IndexElementSize.ThirtyTwo);

        if (lighting != null)
        {
            lighting.BindShadowMaps(pass);
        }

        pass.DrawIndexedPrimitives(_lineIndexCount, (uint)instanceCount, 0, 0, 0);
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
    ) : MoonWorks.Graphics.IVertexType
    {
        /// <inheritdoc cref="P:Microsoft.Xna.Framework.Graphics.IVertexType.VertexDeclaration" />
        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, compat.VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, compat.VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, compat.VertexElementFormat.Vector3, VertexElementUsage.Position, 1),
            new VertexElement(36, compat.VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(40, compat.VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(44, compat.VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(56, compat.VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 2)
        );

        public static ReadOnlySpan<VertexElementFormat> Formats =>
        [
            MoonWorks.Graphics.VertexElementFormat.Float3,     // location 0: Position
            MoonWorks.Graphics.VertexElementFormat.Float3,     // location 1: Normal
            MoonWorks.Graphics.VertexElementFormat.Float3,     // location 2: Centroid
            MoonWorks.Graphics.VertexElementFormat.Ubyte4Norm, // location 3: Color
            MoonWorks.Graphics.VertexElementFormat.Float,      // location 4: DecalOffset
            MoonWorks.Graphics.VertexElementFormat.Float3,     // location 5: Right
            MoonWorks.Graphics.VertexElementFormat.Float3      // location 6: Up
        ];

        public static ReadOnlySpan<uint> Offsets => [0, 12, 24, 36, 40, 44, 56];
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