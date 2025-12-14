using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class LineMesh(
    Mesh supermesh,
    GraphicsDevice graphicsDevice,
    VertexBuffer lineVertexBuffer,
    IndexBuffer lineIndexBuffer,
    int lineTriangleCount
)
{
    private readonly PolyEffect _material = new(Program._polyShader);

    public void Render(Camera camera, Camera? lightCamera, Matrix matrixWorld)
    {
        graphicsDevice.SetVertexBuffer(lineVertexBuffer);
        graphicsDevice.Indices = lineIndexBuffer;
        graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToXnaVector3());
        _material.IsFullbright?.SetValue(false);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Vector3(0, 0, 0));
        _material.LightDirection?.SetValue(World.LightDirection.ToXna());
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity);
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(supermesh.GetsShadowed);
        _material.Alpha?.SetValue(1f);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
        _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position.ToXna());

        _material.CurrentTechnique = _material.Techniques["Basic"];
        
        if (lightCamera != null)
        {
            _material.LightViewProj?.SetValue(lightCamera.ViewProjectionMatrix);
        }

        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, lineTriangleCount);
        }
    }
}