using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Submesh(
    PolyType polyType,
    Mesh supermesh,
    GraphicsDevice graphicsDevice,
    VertexBuffer vertexBuffer,
    IndexBuffer indexBuffer,
    int triangleCount
)
{
    private readonly PolyEffect _material = new(Program._polyShader);

    public void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap, Matrix matrixWorld)
    {
        if (isCreateShadowMap && !(supermesh.CastsShadow || supermesh.Position.Y < World.Ground)) return;

        graphicsDevice.SetVertexBuffer(vertexBuffer);
        graphicsDevice.Indices = indexBuffer;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToXnaVector3());
        _material.IsFullbright?.SetValue(polyType is PolyType.BrakeLight or PolyType.Light or PolyType.ReverseLight && World.LightsOn);
        _material.UseBaseColor?.SetValue(polyType is PolyType.Glass);
        _material.BaseColor?.SetValue(World.Sky.ToXnaVector3());
        _material.LightDirection?.SetValue(World.LightDirection.ToXna());
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToXnaVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity);
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(supermesh.GetsShadowed);
        _material.Alpha?.SetValue(polyType is PolyType.Glass ? 0.1f : 1f);

        if (polyType is PolyType.Glass)
        {
            // Disable z-write for transparent glass
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        }

        if (isCreateShadowMap)
        {
            _material.View?.SetValue(lightCamera!.ViewMatrix);
            _material.Projection?.SetValue(lightCamera!.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * lightCamera!.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * lightCamera!.ViewMatrix * lightCamera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(lightCamera!.Position.ToXna());
        }
        else
        {
            _material.View?.SetValue(camera.ViewMatrix);
            _material.Projection?.SetValue(camera.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(camera.Position.ToXna());
        }

        if (lightCamera != null)
        {
            _material.LightViewProj?.SetValue(lightCamera.ViewProjectionMatrix);
        }

        _material.CurrentTechnique = isCreateShadowMap ? _material.Techniques["CreateShadowMap"] : _material.Techniques["Basic"];
        if (!isCreateShadowMap)
        {
            _material.ShadowMap?.SetValue(Program.shadowRenderTarget);
        }
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, triangleCount);
        }
        
        graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}