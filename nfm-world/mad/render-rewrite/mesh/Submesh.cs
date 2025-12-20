using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Submesh(
    PolyType polyType,
    Mesh supermesh,
    GraphicsDevice graphicsDevice,
    VertexBuffer vertexBuffer,
    IndexBuffer indexBuffer,
    int triangleCount,
    int vertexCount)
{
    private readonly PolyEffect _material = new(Program._polyShader);
    public readonly PolyType PolyType = polyType;

    public void Render(Camera camera, Lighting? lighting, Matrix matrixWorld)
    {
        if (lighting?.IsCreateShadowMap == true && !(supermesh.CastsShadow || supermesh.Position.Y < World.Ground)) return;

        graphicsDevice.SetVertexBuffer(vertexBuffer);
        graphicsDevice.Indices = indexBuffer;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToVector3());
        _material.IsFullbright?.SetValue(PolyType is PolyType.BrakeLight or PolyType.Light or PolyType.ReverseLight && World.LightsOn);
        _material.UseBaseColor?.SetValue(PolyType is PolyType.Glass);
        _material.BaseColor?.SetValue(World.Sky.ToVector3());
        _material.LightDirection?.SetValue(World.LightDirection);
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1f));
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(supermesh.GetsShadowed);
        _material.Alpha?.SetValue(supermesh.alphaOverride ?? (PolyType is PolyType.Glass ? 0.7f : 1f));
        _material.ChargedBlinkAmount?.SetValue(0.0f);

        if (PolyType is PolyType.Glass)
        {
            // Disable z-write for transparent glass
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            graphicsDevice.BlendState = BlendState.NonPremultiplied;
        }

        if (lighting?.IsCreateShadowMap == true)
        {
            _material.View?.SetValue(lighting.CascadeLightCamera.ViewMatrix);
            _material.Projection?.SetValue(lighting.CascadeLightCamera.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * lighting.CascadeLightCamera.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * lighting.CascadeLightCamera.ViewMatrix * lighting.CascadeLightCamera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(lighting.CascadeLightCamera.Position);
        }
        else
        {
            _material.View?.SetValue(camera.ViewMatrix);
            _material.Projection?.SetValue(camera.ProjectionMatrix);
            _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
            _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
            _material.CameraPosition?.SetValue(camera.Position);
        }

        _material.CurrentTechnique = lighting?.IsCreateShadowMap == true ? _material.Techniques["CreateShadowMap"] : _material.Techniques["Basic"];
        
        lighting?.SetShadowMapParameters(_material.UnderlyingEffect);

        _material.Expand?.SetValue(supermesh.Flames.Expand);
        _material.Darken?.SetValue(supermesh.Flames.Darken);
        _material.RandomFloat?.SetValue(URandom.Single());
        
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertexCount, 0, triangleCount);
        }
        
        graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.BlendState = BlendState.Opaque;
    }
}