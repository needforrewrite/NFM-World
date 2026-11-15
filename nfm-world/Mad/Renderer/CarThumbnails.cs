using System.Runtime.InteropServices;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using SignalsDotnet;

namespace NFMWorld;

/// <summary>
/// Bakes each car into a small texture for the garage's cards, one or two a frame.
/// </summary>
/// <remarks>
/// A bake is a scene draw, so it borrows the caller's frame buffer and restores the
/// backbuffer before returning (same contract as <see cref="StageMinimap"/>). The images are
/// kept for the process: they only change when the paint does, which invalidates one entry.
/// </remarks>
internal static class CarThumbnails
{
    private const int Width = 512;
    private const int Height = 256;
    private const int Supersampling = 4;
    private const int RenderWidth = Width * Supersampling;
    private const float Fov = 34f;
    private const float NearPlane = 50f;

    private static readonly Dictionary<string, (IRenderTarget Target, IImage Image, Rad3d Rad)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Signal<bool>> Ready =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Queue<(string Id, Rad3d Rad, int Version)> Pending = new();
    private static readonly Dictionary<string, int> Versions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Queued = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Flips when a card's image is (re)rendered. Cards bind to their own signal.</summary>
    public static Signal<bool> ReadySignal(string id)
    {
        if (!Ready.TryGetValue(id, out var signal))
            Ready[id] = signal = new Signal<bool>(Cache.ContainsKey(id));
        return signal;
    }

    public static IImage? Get(string id) => Cache.TryGetValue(id, out var entry) ? entry.Image : null;

    public static void Request(string id, Rad3d rad)
    {
        if (id.Length == 0 || Cache.ContainsKey(id) || !Queued.Add(id))
            return;

        Pending.Enqueue((id, rad, Versions.GetValueOrDefault(id)));
    }

    public static string WheelKey(string id, bool original) => "$wheel/" + id + (original ? "/nfm" : "/model");

    public static void RequestWheel(string id, Rad3d rad, bool original)
    {
        var key = WheelKey(id, original);
        if (Cache.ContainsKey(key) || Queued.Contains(key) || rad.Wheels.Length == 0) return;
        var source = CarPaint.CreateInstance(rad, wheels: CarWheels.Get(id));
        if (original) CarWheels.Apply(source, new WheelAppearance
        {
            OriginalWheels = true, RimColor = CarWheels.Get(id).RimColor
        });
        else CarWheels.Apply(source, new WheelAppearance { Colors = CarWheels.Get(id).Colors });
        var wheel = source.Wheels.FirstOrDefault(wheel => wheel.Position.X >= 0
                                                          && (original || wheel.Polys is not null)
        );
        if (wheel.Height == 0)
            wheel = source.Wheels.FirstOrDefault(wheel => original || wheel.Polys is not null);
        if (wheel.Height == 0) wheel = source.Wheels[0];
        Request(key, new WheelMeshBuilder(wheel, source.Rims).BuildModel());
    }

    /// <summary>Drops a baked image so the next request renders it again (used after painting).</summary>
    public static void Invalidate(string id)
    {
        if (Cache.Remove(id, out var entry))
            entry.Target.Dispose();

        Queued.Remove(id);
        Versions[id] = Versions.GetValueOrDefault(id) + 1;
        if (Ready.TryGetValue(id, out var signal))
            signal.Value = false;
    }

    public static void InvalidateAll()
    {
        foreach (var (id, entry) in Cache.ToArray())
        {
            Invalidate(id);
            Request(id, entry.Rad);
        }
    }

    /// <summary>Renders at most one queued car. Called from the garage phase's frame.</summary>
    public static void RenderPending(ICommandBuffer cb, Func<string, bool> visible)
    {
        (string Id, Rad3d Rad, int Version) next = default;
        var found = false;
        for (var remaining = Pending.Count; remaining > 0; remaining--)
        {
            var pending = Pending.Dequeue();
            if (pending.Version != Versions.GetValueOrDefault(pending.Id)) continue;
            if (!visible(pending.Id))
            {
                Pending.Enqueue(pending);
                continue;
            }

            next = pending;
            found = true;
            break;
        }

        if (!found) return;
        var (id, rad, _) = next;
        try
        {
            Render(cb, id, rad);
        }
        catch (Exception ex)
        {
            // A model that cannot be drawn only costs its card image.
            Logging.Warning($"Could not render a car thumbnail for '{id}': {ex.Message}");
        }
    }

    private static void Render(ICommandBuffer cb, string id, Rad3d rad)
    {
        var square = id.StartsWith("$wheel/", StringComparison.Ordinal);
        var height = square ? Width : Height;
        var renderHeight = height * Supersampling;
        if (!square)
        {
            rad = CarPaint.CreateInstance(rad);
            CarPaint.Apply(rad, id);
        }

        var target = GameSparker.GraphicsDevice.CreateRenderTarget(
            new RenderTargetDesc(Width, height, TextureFormat.Rgba8, HasDepthStencil: false)
        );

        try
        {
            using var samples = GameSparker.GraphicsDevice.CreateRenderTarget(
                new RenderTargetDesc(RenderWidth, renderHeight, TextureFormat.Rgba8)
            );
            cb.SetRenderTarget(samples);
            cb.SetViewport(new Viewport(0, 0, RenderWidth, renderHeight));
            cb.SetScissorRect(new ScissorRect(0, 0, RenderWidth, renderHeight));
            cb.Clear(ClearOptions.Color | ClearOptions.Depth, new ColorRgba(0, 0, 0, 0));

            using var mesh = new Mesh(rad);
            var body = new MeshedGameObject(mesh) { GetsShadowed = false };
            var wheels = new List<MeshedGameObject>();
            var oldSnap = World.Snap;
            var oldFade = World.FadeFrom;
            var oldThickness = World.OutlineThickness;
            var oldPixels = World.PixelOutlines;
            var oldOutlineBehavior = World.DistantOutlineBehavior;
            try
            {
                foreach (var wheel in rad.Wheels)
                    wheels.Add(new WheelMeshBuilder(wheel, rad.Rims).BuildGameObject(body));
                body.Children = wheels.ToArray();
                body.OnBeforeRender(1f);

                var points = mesh.Polys.SelectMany(poly => poly.Points).ToList();
                foreach (var wheel in wheels)
                    points.AddRange(wheel.Mesh.Polys.SelectMany(poly => poly.Points)
                        .Select(point => point + (Vector3)wheel.Position)
                    );
                var camera = FitCamera(points, height);

                World.Snap = new Color3(0, 0, 0);
                World.FadeFrom = 100_000;
                World.OutlineThickness = 0.75f * Supersampling;
                World.PixelOutlines = false;
                World.DistantOutlineBehavior = DistantOutlineBehavior.AlwaysRender;
                using var queue = new RenderQueue(GameSparker.GraphicsDevice);
                queue.Begin(camera, null);
                body.SubmitDraws(queue, camera, null, RenderPass.Main());
                queue.Flush(cb);
            }
            finally
            {
                World.Snap = oldSnap;
                World.FadeFrom = oldFade;
                World.OutlineThickness = oldThickness;
                World.PixelOutlines = oldPixels;
                World.DistantOutlineBehavior = oldOutlineBehavior;
                foreach (var wheel in wheels)
                    wheel.Mesh.Dispose();
            }

            Downsample(cb, samples.ColorTexture, target, height);
        }
        catch
        {
            target.Dispose();
            throw;
        }
        finally
        {
            cb.SetRenderTarget(null);
            cb.SetViewport(new Viewport(0, 0, GameSparker.Game.Window.Width, GameSparker.Game.Window.Height));
            GameSparker.Game.SetFullScreenScissor(cb);
        }

        Cache[id] = (target, TheGraphics.G.WrapTexture(target.ColorTexture)!, rad);
        ReadySignal(id).Value = true;
    }

    private static void Downsample(ICommandBuffer cb, ITexture source, IRenderTarget target, int height)
    {
        var device = GameSparker.GraphicsDevice;
        var bottom = device.HasBottomLeftFramebufferOrigin ? 0f : 1f;
        var top = device.HasBottomLeftFramebufferOrigin ? 2f : -1f;
        float[] vertices = [-1, -1, 0, bottom, 3, -1, 2, bottom, -1, 3, 0, top];
        var bytes = MemoryMarshal.AsBytes(vertices.AsSpan());
        using var buffer = device.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, bytes.Length),
            bytes
        );
        using var sampler = device.CreateSampler(new SamplerDesc(
            Filter: TextureFilter.Linear,
            AddressU: TextureAddressMode.Clamp,
            AddressV: TextureAddressMode.Clamp
        )
        );
        cb.SetRenderTarget(target);
        cb.SetViewport(new Viewport(0, 0, Width, height));
        cb.SetScissorRect(new ScissorRect(0, 0, Width, height));
        cb.SetPipeline(Effects.ThumbnailPipeline);
        cb.SetVertexBuffer(0, buffer, 16);
        Effects.ThumbnailParameters.TexelSize.SetValue(cb, new Vector2(1f / RenderWidth, 1f / (height * Supersampling)));
        Effects.ThumbnailParameters.Texture.SetValue(cb, source, sampler);
        cb.Draw(0, 1);
    }

    private static PerspectiveCamera FitCamera(IReadOnlyList<Vector3> points, int height)
    {
        var minimum = points.Aggregate(Vector3.Min);
        var maximum = points.Aggregate(Vector3.Max);
        var center = (minimum + maximum) * 0.5f;
        var direction = Vector3.Normalize(new Vector3(0.85f, -0.24f, 0.46f));
        var right = Vector3.Normalize(Vector3.Cross(-direction, -Vector3.UnitY));
        var up = Vector3.Cross(right, -direction);
        var tanVertical = MathF.Tan(Fov * MathF.PI / 360);
        var tanHorizontal = tanVertical * Width / height;
        var distance = 50f;
        foreach (var point in points)
        {
            var relative = point - center;
            var depth = Vector3.Dot(relative, direction);
            distance = MathF.Max(distance, depth + NearPlane * 2);
            distance = MathF.Max(distance, depth + 1.12f * MathF.Max(
                MathF.Abs(Vector3.Dot(relative, right)) / tanHorizontal,
                MathF.Abs(Vector3.Dot(relative, up)) / tanVertical
            )
            );
        }

        var camera = new PerspectiveCamera
        {
            Fov = Fov, Width = RenderWidth, Height = height * Supersampling, Near = NearPlane,
            PositionWithoutInterpolation = center + direction * distance,
            LookAtWithoutInterpolation = center,
            UpWithoutInterpolation = -Vector3.UnitY
        };
        camera.OnBeforeRender(1f);
        return camera;
    }
}
