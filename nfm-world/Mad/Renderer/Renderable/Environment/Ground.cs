using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;

namespace NFMWorld;

public class Ground : Transform, IRenderable, IImmediateRenderElement, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _vertexBuffer;
    private readonly int _triangleCount;

    public override IReadOnlyList<ITransform> ChildTransforms => [];

    public Ground(GraphicsDevice graphicsDevice)
    {
        // Generate a quad on World.Ground extending infinitely in X and Z
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

        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), data.Length, BufferUsage.None)
        {
            Name = "Ground Vertex Buffer",
            Tag = this
        };
        _vertexBuffer.SetDataEXT(data);
        _triangleCount = data.Length / 3;
    }
    
    ~Ground()
    {
        Dispose(false);
    }

    public void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.Ground), this);
    }

    public void Render(Camera cam, Lighting? lt)
    {
        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        Effects.Ground.WorldView?.SetValue(cam.ViewMatrix);
        Effects.Ground.WorldViewProj?.SetValue(cam.ViewMatrix * cam.ProjectionMatrix);

        Effects.Ground.DepthBias?.SetValue(0.00005f);
        Effects.Ground.FogColor?.SetValue((Vector3)World.Fog.Snap(World.Snap));
        Effects.Ground.FogDistance?.SetValue(World.FadeFrom);
        Effects.Ground.FogLogDensity?.SetValue(World.FogLogDensity);

        lt?.SetShadowMapParameters(Effects.Ground.UnderlyingEffect);

        foreach (var pass in Effects.Ground.CurrentTechnique.Passes)
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