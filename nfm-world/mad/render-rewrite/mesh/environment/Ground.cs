using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world.compat;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world.camera;
using nfm_world.shaders;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.mesh.environment;

public class Ground : Transform, IImmediateRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GpuBuffer _vertexBuffer;
    private readonly uint _vertexCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Ground(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        const int size = 1_000_000;
        var color = World.GroundColor.Snap(World.Snap);
        Span<VertexPositionColor> data =
        [
            new(new Vector3(-size, World.Ground, -size), color),
            new(new Vector3(size, World.Ground, -size), color),
            new(new Vector3(-size, World.Ground, size), color),
            new(new Vector3(size, World.Ground, -size), color),
            new(new Vector3(-size, World.Ground, size), color),
            new(new Vector3(size, World.Ground, size), color)
        ];

        _vertexCount = (uint)data.Length;

        _vertexBuffer = GpuBuffer.Create<VertexPositionColor>(graphicsDevice, BufferUsageFlags.Vertex, _vertexCount);
        var transfer = TransferBuffer.Create<VertexPositionColor>(graphicsDevice, TransferBufferUsage.Upload, _vertexCount);
        var span = transfer.Map<VertexPositionColor>(false);
        data.CopyTo(span);
        transfer.Unmap();

        var cmd = graphicsDevice.AcquireCommandBuffer();
        var copyPass = cmd.BeginCopyPass();
        copyPass.UploadToBuffer(
            new TransferBufferLocation(transfer, 0),
            new BufferRegion(_vertexBuffer, 0, _vertexCount * (uint)Marshal.SizeOf<VertexPositionColor>()),
            false);
        cmd.EndCopyPass(copyPass);
        graphicsDevice.Submit(cmd);
        transfer.Dispose();
    }
    
    ~Ground()
    {
        _vertexBuffer.Dispose();
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
        var vertUniforms = new GroundVertexUniforms
        {
            WorldView = camera.ViewMatrix,
            WorldViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
            Fog = fog
        };
        cmd.PushVertexUniformData(vertUniforms);

        // Push fragment uniforms (fog + shadow params)
        var fragUniforms = new GroundFragUniforms
        {
            Fog = fog,
            Shadow = lighting?.ToShadowParams() ?? default
        };
        cmd.PushFragmentUniformData(fragUniforms);

        // Bind pipeline and resources
        pass.BindGraphicsPipeline(Pipelines.Ground);
        pass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));

        // Bind shadow map textures
        if (lighting != null && !lighting.IsCreateShadowMap)
        {
            lighting.BindShadowMaps(pass);
        }

        pass.DrawPrimitives(_vertexCount, 1, 0, 0);
    }
}