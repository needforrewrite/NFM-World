using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world.compat;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world.camera;
using nfm_world.shaders;
using nfm_world.stage;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.mesh;

public class Submesh : IInstancedRenderElement, IDisposable
{
    public readonly PolyType PolyType;
    
    private readonly GpuBuffer _vertexBuffer;
    private readonly GpuBuffer _indexBuffer;
    private readonly uint _indexCount;
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;

    public Submesh(
        PolyType polyType,
        Mesh supermesh,
        GraphicsDevice graphicsDevice,
        ReadOnlySpan<Mesh.VertexPositionNormalColorCentroid> vertices,
        ReadOnlySpan<int> indices)
    {
        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        PolyType = polyType;
        _indexCount = (uint)indices.Length;

        // Create and upload vertex buffer
        _vertexBuffer = GpuBuffer.Create<Mesh.VertexPositionNormalColorCentroid>(
            graphicsDevice, BufferUsageFlags.Vertex, (uint)vertices.Length);
        using var vtxTransfer = TransferBuffer.Create<Mesh.VertexPositionNormalColorCentroid>(
            graphicsDevice, TransferBufferUsage.Upload, (uint)vertices.Length);
        var vtxSpan = vtxTransfer.Map<Mesh.VertexPositionNormalColorCentroid>(false);
        vertices.CopyTo(vtxSpan);
        vtxTransfer.Unmap();

        // Create and upload index buffer
        _indexBuffer = GpuBuffer.Create<int>(graphicsDevice, BufferUsageFlags.Index, _indexCount);
        using var idxTransfer = TransferBuffer.Create<int>(
            graphicsDevice, TransferBufferUsage.Upload, _indexCount);
        var idxSpan = idxTransfer.Map<int>(false);
        indices.CopyTo(idxSpan);
        idxTransfer.Unmap();

        var cmd = graphicsDevice.AcquireCommandBuffer();
        var copyPass = cmd.BeginCopyPass();
        copyPass.UploadToBuffer<Mesh.VertexPositionNormalColorCentroid>(
            vtxTransfer, _vertexBuffer, 0, 0, (uint)vertices.Length, false);
        copyPass.UploadToBuffer<int>(
            idxTransfer, _indexBuffer, 0, 0, _indexCount, false);
        cmd.EndCopyPass(copyPass);
        graphicsDevice.Submit(cmd);
    }

    ~Submesh()
    {
        Dispose(false);
    }

    public void Render(Camera camera, Lighting? lighting, GpuBuffer instanceBuffer, int instanceCount)
    {
        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;

        bool isShadowPass = lighting?.IsCreateShadowMap == true;

        if (isShadowPass)
        {
            // Shadow map pass — use PolyShadow pipeline
            var shadowUniforms = new PolyShadowVertexUniforms
            {
                View = lighting.CascadeLightCamera.ViewMatrix,
                Projection = lighting.CascadeLightCamera.ProjectionMatrix
            };
            cmd.PushVertexUniformData(shadowUniforms);

            pass.BindGraphicsPipeline(Pipelines.PolyShadow);
        }
        else
        {
            // Main pass — use Poly pipeline
            var vertUniforms = new PolyVertexUniforms
            {
                View = camera.ViewMatrix,
                Projection = camera.ProjectionMatrix,
                ViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
                CameraPosition = camera.Position,
                Alpha = PolyType is PolyType.Glass ? 0.7f : 1f,
                SnapColor = (Vector3)World.Snap,
                Darken = _supermesh.Darken,
                LightDirection = World.LightDirection,
                RandomFloat = URandom.Single(),
                EnvironmentLight = new Vector2(World.BlackPoint, World.WhitePoint),
                IsFullbright = (PolyType is PolyType.BrakeLight or PolyType.Light or PolyType.ReverseLight && World.LightsOn),
                UseBaseColor = PolyType is PolyType.Glass,
                BaseColor = (Vector3)World.Sky,
                Expand = _supermesh.Expand,
                Fog = new FogParams
                {
                    Color = (Vector3)World.Fog.Snap(World.Snap),
                    Distance = World.FadeFrom,
                    Density = World.FogDensity / (World.FogDensity + 1f)
                }
            };
            cmd.PushVertexUniformData(vertUniforms);

            var fragUniforms = new PolyFragUniforms
            {
                Shadow = lighting?.ToShadowParams() ?? default
            };
            cmd.PushFragmentUniformData(fragUniforms);

            pass.BindGraphicsPipeline(Pipelines.Poly);

            if (lighting != null)
            {
                lighting.BindShadowMaps(pass);
            }
        }

        // Bind mesh + instance vertex buffers and index buffer
        pass.BindVertexBuffers(0,
            new BufferBinding(_vertexBuffer, 0),
            new BufferBinding(instanceBuffer, 0));
        pass.BindIndexBuffer(new BufferBinding(_indexBuffer, 0), IndexElementSize.ThirtyTwo);

        pass.DrawIndexedPrimitives(_indexCount, (uint)instanceCount, 0, 0, 0);
    }

    private void ReleaseUnmanagedResources()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
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