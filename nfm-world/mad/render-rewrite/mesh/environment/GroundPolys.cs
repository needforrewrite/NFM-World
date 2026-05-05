using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world.compat;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world.camera;
using nfm_world.shaders;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.mesh.environment;

public class GroundPolys : Transform, IImmediateRenderable
{
    private readonly GpuBuffer _vertexBuffer;
    private readonly GpuBuffer _indexBuffer;
    private readonly uint _indexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public GroundPolys(GraphicsDevice graphicsDevice, Rad3dPoly[] polys)
    {
        var triangulation = Array.ConvertAll(polys,
            poly => MeshHelpers.TriangulateIfNeeded(poly.Points));

        var data = new List<VertexPositionColor>();
        var indices = new List<int>();
        
        for (var i = 0; i < polys.Length; i++)
        {
            var poly = polys[i];
            var result = triangulation[i];

            var baseIndex = data.Count;
            foreach (var point in poly.Points)
            {
                var color = poly.Color;
                data.Add(new VertexPositionColor(point, color));
            }

            for (var index = 0; index < result.Triangles.Length; index += 3)
            {
                var i0 = result.Triangles[index];
                var i1 = result.Triangles[index + 1];
                var i2 = result.Triangles[index + 2];

                indices.AddRange(i0 + baseIndex, i1 + baseIndex, i2 + baseIndex);
            }
        }

        _indexCount = (uint)indices.Count;
        var vtxCount = (uint)data.Count;

        _vertexBuffer = GpuBuffer.Create<VertexPositionColor>(graphicsDevice, BufferUsageFlags.Vertex, vtxCount);
        _indexBuffer = GpuBuffer.Create<int>(graphicsDevice, BufferUsageFlags.Index, _indexCount);

        var vtxTransfer = TransferBuffer.Create<VertexPositionColor>(graphicsDevice, TransferBufferUsage.Upload, vtxCount);
        var vtxSpan = vtxTransfer.Map<VertexPositionColor>(false);
        CollectionsMarshal.AsSpan(data).CopyTo(vtxSpan);
        vtxTransfer.Unmap();

        var idxTransfer = TransferBuffer.Create<int>(graphicsDevice, TransferBufferUsage.Upload, _indexCount);
        var idxSpan = idxTransfer.Map<int>(false);
        CollectionsMarshal.AsSpan(indices).CopyTo(idxSpan);
        idxTransfer.Unmap();

        var cmd = graphicsDevice.AcquireCommandBuffer();
        var copyPass = cmd.BeginCopyPass();
        copyPass.UploadToBuffer(
            new TransferBufferLocation(vtxTransfer, 0),
            new BufferRegion(_vertexBuffer, 0, vtxCount * (uint)Marshal.SizeOf<VertexPositionColor>()),
            false);
        copyPass.UploadToBuffer(
            new TransferBufferLocation(idxTransfer, 0),
            new BufferRegion(_indexBuffer, 0, _indexCount * sizeof(int)),
            false);
        cmd.EndCopyPass(copyPass);
        graphicsDevice.Submit(cmd);

        vtxTransfer.Dispose();
        idxTransfer.Dispose();
    }
    
    ~GroundPolys()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }

    public void Render(Camera camera, Lighting? lighting = null)
    {
        if (lighting?.IsCreateShadowMap == true) return;

        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;

        var fog = new FogParams
        {
            Color = (Vector3)World.Fog.Snap(World.Snap),
            Distance = World.FadeFrom,
            Density = World.FogDensity / (World.FogDensity + 1f)
        };

        cmd.PushVertexUniformData(new GroundVertexUniforms
        {
            WorldView = camera.ViewMatrix,
            WorldViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
            Fog = fog
        });

        cmd.PushFragmentUniformData(new GroundFragUniforms
        {
            Fog = fog,
            Shadow = lighting?.ToShadowParams() ?? default
        });

        pass.BindGraphicsPipeline(Pipelines.Ground);
        pass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));
        pass.BindIndexBuffer(new BufferBinding(_indexBuffer, 0), IndexElementSize.ThirtyTwo);

        if (lighting != null && !lighting.IsCreateShadowMap)
        {
            lighting.BindShadowMaps(pass);
        }

        pass.DrawIndexedPrimitives(_indexCount, 1, 0, 0, 0);
    }
}