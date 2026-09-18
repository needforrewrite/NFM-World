using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class Sky : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly int _triangleCount;
    
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
            data.Add(new VertexPositionColor(vertices[0].Position, new Color(vertices[0].Color).ToXna()));
            data.Add(new VertexPositionColor(vertices[1].Position, new Color(vertices[1].Color).ToXna()));
            data.Add(new VertexPositionColor(vertices[2].Position, new Color(vertices[2].Color).ToXna()));
            data.Add(new VertexPositionColor(vertices[1].Position, new Color(vertices[1].Color).ToXna()));
            data.Add(new VertexPositionColor(vertices[2].Position, new Color(vertices[2].Color).ToXna()));
            data.Add(new VertexPositionColor(vertices[3].Position, new Color(vertices[3].Color).ToXna()));
        }

        var vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Count, BufferUsage.None)
        {
            Name = "Sky Vertex Buffer",
            Tag = this
        };
        vertexBuffer.SetDataEXT(data);
        _vertexBuffer = vertexBuffer;

        _triangleCount = data.Count / 3;
        return;

        static float Fade(int i) {
            return World.FadeFrom / 2f * (i + 1);
        }
    }

    ~Sky()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.Sky), this);
    }

    public void Render(Camera cam, Lighting? _)
    {
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.DepthStencilState = DepthStencilState.None;

        Vector3 col = World.Sky.Snap(World.Snap);
        for (var i = 1; i < 20; ++i)
        {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
        }

        _graphicsDevice.Clear(new Color(col).ToXna());

        // Extract camera rotation from view direction
        var viewDirection = Vector3.Normalize(cam.LookAt - cam.Position);

        // Calculate yaw from view direction
        var yaw = (float)Math.Atan2(viewDirection.X, viewDirection.Z);

        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -yaw);
        var fullRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
        var combinedRotation = yawRotation * fullRotation;
        combinedRotation = Quaternion.Inverse(combinedRotation);

        var viewMatrix = Matrix.CreateFromQuaternion(combinedRotation);

        Effects.Sky.Parameters["WorldViewProj"]?.SetValue(viewMatrix * cam.ProjectionMatrix);
        foreach (var pass in Effects.Sky.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangleCount);
        }

        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private void ReleaseUnmanagedResources()
    {
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
            _vertexBuffer.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}