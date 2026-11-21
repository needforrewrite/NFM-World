using System.Diagnostics;
using System.Runtime.InteropServices;
using NFMWorld.Graphics;

namespace NFMWorld;

internal sealed class Stars : IDisposable
{
    private const int Segments = 24;
    private static readonly Vector2[] Circle = Enumerable.Range(0, Segments + 1)
        .Select(i => new Vector2(MathF.Cos(i * MathF.Tau / Segments), MathF.Sin(i * MathF.Tau / Segments)))
        .ToArray();

    private struct Star
    {
        public Vector3 Position;
        public Vector3 Tint;
        public bool Large;
        public float Phase;
        public float TwinkleSpeed;
    }

    private readonly Star[] _stars = new Star[40];
    private readonly PositionColorVertex[] _vertices = new PositionColorVertex[40 * Segments * 15];
    private readonly IBuffer _vertexBuffer;
    private readonly IPipelineState _pipeline;
    private readonly Random _random = new(0);
    private readonly long _started = Stopwatch.GetTimestamp();
    private int _vertexCount;

    public Stars(IGraphicsDevice graphicsDevice)
    {
        for (var i = 0; i < _stars.Length; i++)
            _stars[i] = new Star
            {
                Position = new Vector3(_random.NextSingle() * 2000 - 1000, -200, _random.NextSingle() * 2000 - 1000),
                Tint = NewTint(),
                Large = _random.NextSingle() > 0.8f,
                Phase = _random.NextSingle() * MathF.Tau,
                TwinkleSpeed = 1.4f + _random.NextSingle() * 1.8f
            };

        var bytes = _vertices.Length * PositionColorVertex.Stride;
        _vertexBuffer = graphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, bytes),
            new byte[bytes]
        );
        _pipeline = graphicsDevice.CreatePipeline(
            new PipelineDesc(
                VertexShader: Effects.SkyModule,
                PixelShader: Effects.SkyModule,
                VertexLayouts: [PositionColorVertex.VertexLayout],
                BlendState: BlendStateDesc.NonPremultiplied,
                DepthStencilState: DepthStencilStateDesc.None,
                RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None, ScissorTestEnabled = true }
            )
        );
    }

    private Vector3 NewTint()
    {
        var primary = _random.Next(3);
        var secondary = (primary + (_random.Next(2) == 0 ? 1 : 2)) % 3;
        Span<float> channels = [200, 200, 200];
        channels[primary] += _random.NextSingle() * 55;
        channels[secondary] += 55;
        return new Vector3(channels[0], channels[1], channels[2]) / 255;
    }

    public void Render(ICommandBuffer cb, Camera camera)
    {
        if (camera.Width <= 0 || camera.Height <= 0) return;

        var view = camera.ViewMatrix;
        view.Translation = Vector3.Zero;
        var viewProjection = view * camera.ProjectionMatrix;
        var time = (float)Stopwatch.GetElapsedTime(_started).TotalSeconds;
        var scale = MathF.Max(1, MathF.Sqrt(camera.Height / 720f));
        _vertexCount = 0;

        foreach (ref var star in _stars.AsSpan())
        {
            var clip = Vector4.Transform(new Vector4(star.Position, 1), viewProjection);
            if (clip.W <= 0) continue;
            var x = clip.X / clip.W;
            var y = clip.Y / clip.W;
            if (x < -1 || x > 1 || y < -1 || y > 1) continue;

            var core = Vector3.Lerp(star.Tint, Vector3.One, 0.5f);
            var brightness = 0.88f + 0.12f * MathF.Sin(time * star.TwinkleSpeed + star.Phase);
            var radius = (star.Large ? 3.2f : 2f) * scale;
            var pixelX = (x + 1) * camera.Width / 2;
            var pixelY = (1 - y) * camera.Height / 2;
            AddGlow(camera, new Vector2(pixelX, pixelY), radius, core, brightness);
        }

        if (_vertexCount == 0) return;
        cb.SetPipeline(_pipeline);
        cb.UpdateBuffer(_vertexBuffer, MemoryMarshal.AsBytes(_vertices.AsSpan(0, _vertexCount)));
        cb.SetVertexBuffer(0, _vertexBuffer, PositionColorVertex.Stride);
        Effects.SkyParameters.WorldViewProj.SetValue(cb, Matrix.Identity);
        cb.Draw(startVertex: 0, primitiveCount: _vertexCount / 3);
    }

    private void AddGlow(Camera camera, Vector2 center, float radius, Vector3 tint, float brightness)
    {
        ReadOnlySpan<float> radii = [0, 0.3f, 0.55f, 1];
        ReadOnlySpan<float> opacity = [1, 0.86f, 0.28f, 0];
        for (var ring = 0; ring < radii.Length - 1; ring++)
        {
            var inner = radius * radii[ring];
            var outer = radius * radii[ring + 1];
            var innerColor = new Color(tint, opacity[ring] * brightness);
            var outerColor = new Color(tint, opacity[ring + 1] * brightness);
            for (var segment = 0; segment < Segments; segment++)
            {
                var a = Circle[segment];
                var b = Circle[segment + 1];
                AddVertex(camera, center + a * inner, innerColor);
                AddVertex(camera, center + a * outer, outerColor);
                AddVertex(camera, center + b * outer, outerColor);
                if (ring == 0) continue;
                AddVertex(camera, center + a * inner, innerColor);
                AddVertex(camera, center + b * outer, outerColor);
                AddVertex(camera, center + b * inner, innerColor);
            }
        }
    }

    private void AddVertex(Camera camera, Vector2 pixel, Color color)
    {
        var position = new Vector3(pixel.X * 2 / camera.Width - 1, 1 - pixel.Y * 2 / camera.Height, 0);
        _vertices[_vertexCount++] = new(position, color);
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _pipeline.Dispose();
    }
}
