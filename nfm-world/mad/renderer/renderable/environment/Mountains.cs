using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world.camera;
using nfm_world.compat;
using nfm_world.gameobject;
using nfm_world.renderable.mesh.utils;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.renderable.environment;

public class Mountains : Transform, IImmediateRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GpuBuffer _vertexBuffer;
    private readonly GpuBuffer _indexBuffer;
    private readonly uint _indexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Mountains(GraphicsDevice graphicsDevice, Rad3dPoly[] polys)
    {
        _graphicsDevice = graphicsDevice;
        
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

        var vtxCount = (uint)data.Count;
        _indexCount = (uint)indices.Count;

        // Create and upload vertex buffer
        _vertexBuffer = GpuBuffer.Create<VertexPositionColor>(graphicsDevice, BufferUsageFlags.Vertex, vtxCount);
        {
            var transfer = TransferBuffer.Create<VertexPositionColor>(graphicsDevice, TransferBufferUsage.Upload, vtxCount);
            var span = transfer.Map<VertexPositionColor>(false);
            CollectionsMarshal.AsSpan(data).CopyTo(span);
            transfer.Unmap();

            var cmd = graphicsDevice.AcquireCommandBuffer();
            var copyPass = cmd.BeginCopyPass();
            copyPass.UploadToBuffer(
                new TransferBufferLocation(transfer, 0),
                new BufferRegion(_vertexBuffer, 0, vtxCount * (uint)Marshal.SizeOf<VertexPositionColor>()),
                false);
            cmd.EndCopyPass(copyPass);
            graphicsDevice.Submit(cmd);
            transfer.Dispose();
        }

        // Create and upload index buffer
        _indexBuffer = GpuBuffer.Create<int>(graphicsDevice, BufferUsageFlags.Index, _indexCount);
        {
            var transfer = TransferBuffer.Create<int>(graphicsDevice, TransferBufferUsage.Upload, _indexCount);
            var span = transfer.Map<int>(false);
            CollectionsMarshal.AsSpan(indices).CopyTo(span);
            transfer.Unmap();

            var cmd = graphicsDevice.AcquireCommandBuffer();
            var copyPass = cmd.BeginCopyPass();
            copyPass.UploadToBuffer(
                new TransferBufferLocation(transfer, 0),
                new BufferRegion(_indexBuffer, 0, _indexCount * sizeof(int)),
                false);
            cmd.EndCopyPass(copyPass);
            graphicsDevice.Submit(cmd);
            transfer.Dispose();
        }
    }

    ~Mountains()
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

        // Push vertex uniforms
        var fog = new FogParams
        {
            Color = (Vector3)World.Fog.Snap(World.Snap),
            Distance = World.FadeFrom,
            Density = World.FogDensity / (World.FogDensity + 1f)
        };
        var vertUniforms = new MountainsVertexUniforms
        {
            WorldView = camera.ViewMatrix,
            WorldViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
            Fog = fog
        };
        cmd.PushVertexUniformData(vertUniforms);

        // Push fragment uniforms
        var fragUniforms = new MountainsFragUniforms
        {
            Shadow = lighting?.ToShadowParams() ?? default
        };
        cmd.PushFragmentUniformData(fragUniforms);

        // Bind pipeline and resources
        pass.BindGraphicsPipeline(Pipelines.Mountains);
        pass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));
        pass.BindIndexBuffer(new BufferBinding(_indexBuffer, 0), IndexElementSize.ThirtyTwo);

        if (lighting != null && !lighting.IsCreateShadowMap)
        {
            lighting.BindShadowMaps(pass);
        }

        pass.DrawIndexedPrimitives(_indexCount, 1, 0, 0, 0);
    }
}