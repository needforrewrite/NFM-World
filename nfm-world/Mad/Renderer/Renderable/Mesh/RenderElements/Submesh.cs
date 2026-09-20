﻿using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;

namespace NFMWorld;

public class Submesh : IInstancedRenderElement, IDisposable
{
    public readonly PolyType PolyType;

    private readonly IBuffer _vertexBuffer;
    private readonly IBuffer _indexBuffer;

    private readonly int _vertexCount;
    private readonly int _triangleCount;
    private readonly Mesh _supermesh;
    private readonly IGraphicsDevice _graphicsDevice;

    public Submesh(
        PolyType polyType,
        Mesh supermesh,
        IGraphicsDevice graphicsDevice,
        ReadOnlySpan<Mesh.VertexPositionNormalColorCentroid> vertices,
        ReadOnlySpan<uint> indices)
    {
        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        PolyType = polyType;

        var vertexBytes = MemoryMarshal.AsBytes(vertices);
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length), vertexBytes);

        var indexBytes = MemoryMarshal.AsBytes(indices);
        _indexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indexBytes.Length, IndexFormat.UInt32), indexBytes);

        _vertexCount = vertices.Length;
        _triangleCount = indices.Length / 3;
    }

    ~Submesh()
    {
        Dispose(false);
    }

    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting, IBuffer instanceBuffer, int instanceCount)
    {
        // Picks between the pipelines built from Poly.fx (see Effects.cs's remarks): the
        // "CreateShadowMap" technique for a cascade, the editor's translucent overlays when the
        // mesh asks for one, and the game's own opaque "Basic" pipeline otherwise. The shadow pass
        // wins over the overlay mode so an editor flag can never leak into a cascade.
        var isShadowPass = lighting?.IsCreateShadowMap == true;
        var (pipeline, p) = (isShadowPass, _supermesh.OverlayMode) switch
        {
            (true, _) => (Effects.PolyShadowPipeline, Effects.PolyShadowParameters),
            (_, PolyOverlayMode.DepthRead) => (Effects.PolyDepthReadPipeline, Effects.PolyDepthReadParameters),
            (_, PolyOverlayMode.NoDepth) => (Effects.PolyNoDepthPipeline, Effects.PolyNoDepthParameters),
            _ => (Effects.PolyPipeline, Effects.PolyParameters),
        };

        cb.SetPipeline(pipeline);
        cb.SetVertexBuffer(0, _vertexBuffer, Mesh.VertexPositionNormalColorCentroid.VertexLayout.StrideInBytes);
        cb.SetVertexBuffer(1, instanceBuffer, InstanceData.Stride);
        cb.SetIndexBuffer(_indexBuffer);

        // CreateShadowMapVS transforms by the cascade's *light* camera, not the view camera: the
        // shadow map holds the depths the sun sees, and the main pass re-projects each pixel with
        // that same LightViewProj to compare against them. (The pre-migration Submesh.Render
        // swapped the camera here the same way.) Feeding it the view camera instead puts unrelated
        // depths in the map, and that comparison then simply never triggers - i.e. no shadows.
        var passCamera = isShadowPass ? lighting!.CascadeLightCamera! : camera;
        p.View.SetValue(cb, passCamera.ViewMatrix);
        p.Projection.SetValue(cb, passCamera.ProjectionMatrix);

        if (isShadowPass)
        {
            // CreateShadowMapVS only reads View/Projection and the per-instance world matrix.
            cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: _triangleCount, instanceCount: instanceCount);
            return;
        }

        p.ViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.SnapColor.SetValue(cb, World.Snap);
        p.IsFullbright.SetValue(cb, PolyType is PolyType.BrakeLight or PolyType.Light or PolyType.ReverseLight && World.LightsOn);
        p.UseBaseColor.SetValue(cb, PolyType is PolyType.Glass or PolyType.CGround);
        p.BaseColor.SetValue(cb, PolyType is PolyType.CGround ? World.GroundColor : World.Sky);
        p.FogColor.SetValue(cb, World.Fog.Snap(World.Snap));
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);
        p.EnvironmentLight.SetValue(cb, new Vector2(World.BlackPoint, World.WhitePoint));
        p.CameraPosition.SetValue(cb, camera.Position);
        p.Alpha.SetValue(cb, PolyType is PolyType.Glass ? 0.7f : 1f);
        p.Expand.SetValue(cb, _supermesh.Expand);
        p.RandomFloat.SetValue(cb, URandom.Single());
        p.Darken.SetValue(cb, _supermesh.Darken);

        if (_supermesh.PolyFixState > 0)
        {
            const float hsb0 = 0.57F;
            const float hsb2 = 0.8F;
            const float hsb1 = 0.8F;
            var color = Color3.FromHSB(hsb0, hsb1, hsb2);
            var r = (short)(color.R + color.R * (World.Snap[0] / 100.0F));
            if (r > 255) r = 255;
            if (r < 0) r = 0;
            var g = (short)(color.G + color.G * (World.Snap[1] / 100.0F));
            if (g > 255) g = 255;
            if (g < 0) g = 0;
            var b = (short)(color.B + color.B * (World.Snap[2] / 100.0F));
            if (b > 255) b = 255;
            if (b < 0) b = 0;
            p.UseBaseColor.SetValue(cb, true);
            p.BaseColor.SetValue(cb, new Color3(r, g, b));
        }

        // Shadow-map depth bias - the shader's own default (0.0005) is an order of magnitude too
        // large for the light cameras' depth range (Near 50 / Far 1000000 normalizes to ~d/1e6, so
        // 0.0005 already means ~500 world units of occlusion) and leaves almost everything lit.
        // Every pre-migration consumer of Poly.fx set this same value (see LineMesh/Ground/Mountains).
        p.DepthBias.SetValue(cb, 0.00005f);

        lighting?.SetShadowMapParameters(cb, pipeline.Reflection);

        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: _triangleCount, instanceCount: instanceCount);
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
