using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world.camera;
using nfm_world.compat;
using nfm_world.gameobject;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.renderable.environment;

public class Sky : Transform, IImmediateRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GpuBuffer _vertexBuffer;
    private readonly uint _vertexCount;
    
    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Sky(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var skyline = -300;

        var layers = new LinkedList<(Vector3 Position, Vector3 Color)>();
        layers.AddLast((new Vector3(0, skyline - 700, 7000), World.Sky.Snap(World.Snap)));

        Vector3 col = World.Sky.Snap(World.Snap);
        for (var i = 0; i < 16; ++i) {
            col = ((new Vector3(7, 7, 7) * col) + World.Fog) / (new Vector3(8, 8, 8));
            layers.AddLast((new Vector3(0, skyline, Fade(i)), col));
        }

        col = World.Sky.Snap(World.Snap);
        for (var i = 1; i < 20; ++i) {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
            layers.AddFirst((new Vector3(0, skyline - 700 - i * 70, 7000), col));
        }
        layers.AddLast((new Vector3(0, 10250, 7000), World.Fog));

        var data = new List<VertexPositionColor>();

        var layersArr = layers.ToArray();
        for (var i = 0; i + 1 < layers.Count; ++i) {
            ReadOnlySpan<(Vector3 Position, Vector3 Color)> vertices = [
                (new Vector3(-1e5f, -layersArr[i].Position.Y, -layersArr[i].Position.Z), layersArr[i].Color),
                (new Vector3(1e5f, -layersArr[i].Position.Y, -layersArr[i].Position.Z), layersArr[i].Color),
                (new Vector3(-1e5f, -layersArr[i + 1].Position.Y, -layersArr[i + 1].Position.Z), layersArr[i + 1].Color),
                (new Vector3(1e5f, -layersArr[i + 1].Position.Y, -layersArr[i + 1].Position.Z), layersArr[i + 1].Color),
            ];
            data.Add(new VertexPositionColor(vertices[0].Position, new Microsoft.Xna.Framework.Color(vertices[0].Color)));
            data.Add(new VertexPositionColor(vertices[1].Position, new Microsoft.Xna.Framework.Color(vertices[1].Color)));
            data.Add(new VertexPositionColor(vertices[2].Position, new Microsoft.Xna.Framework.Color(vertices[2].Color)));
            data.Add(new VertexPositionColor(vertices[1].Position, new Microsoft.Xna.Framework.Color(vertices[1].Color)));
            data.Add(new VertexPositionColor(vertices[2].Position, new Microsoft.Xna.Framework.Color(vertices[2].Color)));
            data.Add(new VertexPositionColor(vertices[3].Position, new Microsoft.Xna.Framework.Color(vertices[3].Color)));
        }

        _vertexCount = (uint)data.Count;

        // Create GPU buffer and upload vertex data
        _vertexBuffer = GpuBuffer.Create<VertexPositionColor>(graphicsDevice, BufferUsageFlags.Vertex, _vertexCount);
        var transfer = TransferBuffer.Create<VertexPositionColor>(graphicsDevice, TransferBufferUsage.Upload, _vertexCount);
        var span = transfer.Map<VertexPositionColor>(false);
        CollectionsMarshal.AsSpan(data).CopyTo(span);
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

        return;

        static float Fade(int i) {
            return World.FadeFrom / 2f * (i + 1);
        }
    }

    ~Sky()
    {
        _vertexBuffer.Dispose();
    }
    
    public void Render(Camera camera, Lighting? lighting = null)
    {
        if (lighting?.IsCreateShadowMap == true) return;

        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;
        
        // Extract camera rotation from view direction
        var viewDirection = Vector3.Normalize(camera.LookAt - camera.Position);
        var yaw = (float)Math.Atan2(viewDirection.X, viewDirection.Z);
        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -yaw);
        var fullRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
        var combinedRotation = yawRotation * fullRotation;
        combinedRotation = Quaternion.Inverse(combinedRotation);
        var viewMatrix = Matrix.CreateFromQuaternion(combinedRotation);

        // Push vertex uniforms
        var uniforms = new SkyVertexUniforms
        {
            WorldViewProj = viewMatrix * camera.ProjectionMatrix
        };
        cmd.PushVertexUniformData(uniforms);

        // Bind pipeline and draw
        pass.BindGraphicsPipeline(Pipelines.Sky);
        pass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));
        pass.DrawPrimitives(_vertexCount, 1, 0, 0);
    }
}