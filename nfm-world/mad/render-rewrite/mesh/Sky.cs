using System.Numerics;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Sky : Transform, IImmediateRenderable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly Effect _material;
    private readonly int _triangleCount;

    public Sky(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;

        var skyline = -300;

        var layers = new LinkedList<(Vector3 Position, Vector3 Color)>();
        layers.AddLast((new Vector3(0, skyline - 700, 7000), World.Sky.Snap(World.Snap).ToVector3()));

        var col = World.Sky.Snap(World.Snap).ToVector3();
        for (var i = 0; i < 16; ++i) {
            col = ((new Vector3(7, 7, 7) * col) + World.Fog.ToVector3()) / (new Vector3(8, 8, 8));
            layers.AddLast((new Vector3(0, skyline, Fade(i)), col));
        }

        col = World.Sky.Snap(World.Snap).ToVector3();
        for (var i = 1; i < 20; ++i) {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
            layers.AddFirst((new Vector3(0, skyline - 700 - i * 70, 7000), col));
        }
        layers.AddLast((new Vector3(0, 250, 7000), World.Fog.ToVector3()));

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

        var vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Count, BufferUsage.None);
        vertexBuffer.SetDataEXT(data);
        _vertexBuffer = vertexBuffer;

        _material = Program._skyShader;
        
        _triangleCount = data.Count / 3;
        return;

        static float Fade(int i) {
            return World.FadeFrom / 2f * (i + 1);
        }
    }
    
    public void Render(Camera camera, Lighting? lighting = null)
    {
        if (lighting?.IsCreateShadowMap == true) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;

        _graphicsDevice.DepthStencilState = DepthStencilState.None;
        
        var col = World.Sky.Snap(World.Snap).ToVector3();
        for (var i = 1; i < 20; ++i) {
            col = new Vector3(0.991f, 0.991f, 0.998f) * col;
        }
        _graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(col));
        
        // Extract camera rotation from view direction
        var viewDirection = Vector3.Normalize(camera.LookAt - camera.Position);
        
        // Calculate yaw from view direction
        var yaw = (float)Math.Atan2(viewDirection.X, viewDirection.Z);
        
        // Create rotation: first rotate by negative yaw, then apply full camera rotation
        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -yaw);
        var fullRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
        var combinedRotation = yawRotation * fullRotation;
        combinedRotation = Quaternion.Inverse(combinedRotation);
        
        var viewMatrix = Matrix.CreateFromQuaternion(combinedRotation);
        
        _material.Parameters["WorldViewProj"]?.SetValue(viewMatrix * camera.ProjectionMatrix);
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            _graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangleCount);
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}