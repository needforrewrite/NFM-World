using Microsoft.Xna.Framework.Graphics;
using Stride.Core.Mathematics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace NFMWorld.Mad;

public class Sky : Transform
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly Effect _material;
    private readonly int _triangleCount;

    public Sky(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        float fade(int i) {
            return World.FadeFrom / 2 * (i + 1);
        }

        var skyline = -300;

        var layers = new LinkedList<(Vector3 Position, Vector3 Color)>();
        layers.AddLast((new Vector3(0, skyline - 700, 0), World.Sky.Snap(World.Snap).ToVector3()));

        var col = World.Sky.Snap(World.Snap).ToVector3();
        for (var i = 0; i < 16; ++i) {
            col = ((new Vector3(7, 7, 7) * col) + World.Fog.ToVector3()) / (new Vector3(8, 8, 8));
            layers.AddLast((new Vector3(0, skyline, fade(i)), col));
        }

        col = World.Sky.Snap(World.Snap).ToVector3();
        for (var i = 1; i < 20; ++i) {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
            layers.AddFirst((new Vector3(0, skyline - 700 - i * 70, 0), col));
        }
        layers.AddLast((new Vector3(0, 250, 0), World.Fog.ToVector3()));

        var data = new List<VertexPositionColor>();

        var layersArr = layers.ToArray();
        for (var i = 0; i + 1 < layers.Count; ++i) {
            ReadOnlySpan<(Vector3 Position, Vector3 Color)> vertices = [
                (new Vector3(-1e5f, -layersArr[i].Position.Y, -layersArr[i].Position.Z), layersArr[i].Color),
                (new Vector3(1e5f, -layersArr[i].Position.Y, -layersArr[i].Position.Z), layersArr[i].Color),
                (new Vector3(-1e5f, -layersArr[i + 1].Position.Y, -layersArr[i + 1].Position.Z), layersArr[i + 1].Color),
                (new Vector3(1e5f, -layersArr[i + 1].Position.Y, -layersArr[i + 1].Position.Z), layersArr[i + 1].Color),
            ];
            data.Add(new VertexPositionColor(vertices[0].Position.ToXna(), new Microsoft.Xna.Framework.Color(vertices[0].Color.ToXna())));
            data.Add(new VertexPositionColor(vertices[1].Position.ToXna(), new Microsoft.Xna.Framework.Color(vertices[1].Color.ToXna())));
            data.Add(new VertexPositionColor(vertices[2].Position.ToXna(), new Microsoft.Xna.Framework.Color(vertices[2].Color.ToXna())));
            data.Add(new VertexPositionColor(vertices[1].Position.ToXna(), new Microsoft.Xna.Framework.Color(vertices[1].Color.ToXna())));
            data.Add(new VertexPositionColor(vertices[2].Position.ToXna(), new Microsoft.Xna.Framework.Color(vertices[2].Color.ToXna())));
            data.Add(new VertexPositionColor(vertices[3].Position.ToXna(), new Microsoft.Xna.Framework.Color(vertices[3].Color.ToXna())));
        }

        var vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Count, BufferUsage.None);
        vertexBuffer.SetData(data.ToArray());
        _vertexBuffer = vertexBuffer;

        _material = Program._skyShader;
        
        _triangleCount = data.Count / 3;
    }
    
    public void Render(Camera camera)
    {
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        
        // Extract camera rotation from view direction
        var viewDirection = Vector3.Normalize(camera.LookAt - camera.Position);
        
        // Calculate yaw from view direction
        var yaw = (float)System.Math.Atan2(viewDirection.X, viewDirection.Z);
        
        // Create rotation: first rotate by negative yaw, then apply full camera rotation
        var yawRotation = Quaternion.RotationY(-yaw);
        var fullRotation = Quaternion.RotationYawPitchRoll(yaw, 0, 0);
        var combinedRotation = yawRotation * fullRotation;
        combinedRotation.Invert();
        
        var viewMatrix = Matrix.CreateFromQuaternion(combinedRotation.ToXna());
        
        _material.Parameters["WorldViewProj"]?.SetValue(viewMatrix * camera.ProjectionMatrix);
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}