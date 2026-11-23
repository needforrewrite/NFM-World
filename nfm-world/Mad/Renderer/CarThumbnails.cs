using System.Runtime.InteropServices;
using System.Diagnostics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.Content;
using NFMWorldLibrary.Radpack;
using SignalsDotnet;

namespace NFMWorld;

/// <summary>
/// Bakes each car into a small texture for the garage's cards, one or two a frame.
/// </summary>
/// <remarks>
/// A bake is a scene draw, so it borrows the caller's frame buffer and restores the
/// backbuffer before returning (same contract as <see cref="StageMinimap"/>).
/// </remarks>
internal static class CarThumbnails
{
    private const int Width = 512;
    private const int Height = 256;
    private const int Supersampling = 4;
    private const int RenderWidth = Width * Supersampling;
    private const float Fov = 34f;
    private const float NearPlane = 50f;
    private const float FrameFill = 0.88f;

    private sealed record Entry(IRenderTarget Target, IImage Image, Rad3d Rad)
    {
        public long Used = Stopwatch.GetTimestamp();
    }

    private static readonly Dictionary<string, Entry> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, WeakReference<Signal<bool>>> Ready =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Queue<(string Id, Rad3d Rad, int Version, long ReadyAfterFrame)> Pending = new();
    private static readonly List<(IRenderTarget Target, long AfterFrame)> Retired = new();
    private static readonly Dictionary<string, int> Versions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Queued = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<object> Users = new(ReferenceEqualityComparer.Instance);
    private static int _nextVersion;
    private static int _generation;
    private static readonly HashSet<string> LoadingModels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Flips when a card's image is (re)rendered. Cards bind to their own signal.</summary>
    public static Signal<bool> ReadySignal(string id)
    {
        if (Ready.TryGetValue(id, out var reference) && reference.TryGetTarget(out var current)) return current;
        var signal = new Signal<bool>(Cache.ContainsKey(id));
        Ready[id] = new(signal);
        return signal;
    }

    public static IImage? Get(string id)
    {
        if (!Cache.TryGetValue(id, out var entry)) return null;
        entry.Used = Stopwatch.GetTimestamp();
        return entry.Image;
    }

    public static void BeginUse(object owner) => Users.Add(owner);

    public static void EndUse(object owner)
    {
        Users.Remove(owner);
        if (Users.Count != 0) return;
        _generation++;
        LoadingModels.Clear();
        Pending.Clear();
        Queued.Clear();
        Versions.Clear();
    }

    public static void Request(string id, Rad3d rad)
    {
        if (id.Length == 0 || Cache.ContainsKey(id) || !Queued.Add(id))
            return;

        var version = Versions[id] = ++_nextVersion;
        Pending.Enqueue((id, rad, version, LoadingWork.FrameId + 1));
    }

    public static void RequestKnown(string id)
    {
        if (Cache.ContainsKey(id) || Queued.Contains(id) || LoadingModels.Contains(id)) return;
        if (BackendGameSparker.FindLoadedCar(id) is { } model) Request(id, model);
        else if (RadpackReader.IsArchiveId(id) && ContentCache.IsPrepared(id))
        {
            LoadingModels.Add(id);
            _ = LoadPreparedModelAsync(id, _generation);
        }
    }

    private static async Task LoadPreparedModelAsync(string id, int generation)
    {
        using var loading = LoadingWork.Begin("Loading car preview...");
        try
        {
            var model = await Task.Run(() => RadpackReader.TryReadModel(id, RadpackType.Car, out _));
            await LoadingWork.NextFrame;
            if (generation == _generation && Users.Count > 0 && model is not null) Request(id, model);
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not load car thumbnail '{id}': {ex.Message}");
        }
        finally
        {
            await LoadingWork.NextFrame;
            if (generation == _generation) LoadingModels.Remove(id);
        }
    }

    public static string WheelKey(string id, bool original) => "$wheel/" + id + (original ? "/nfm" : "/model");

    public static void RequestWheel(string id, Rad3d rad, bool original)
    {
        var key = WheelKey(id, original);
        if (Cache.ContainsKey(key) || Queued.Contains(key) || rad.Wheels.Length == 0) return;
        var style = CarWheels.Get(id);
        style.OriginalWheels = original;
        var source = CarPaint.CreateInstance(rad, wheels: style);
        var wheels = source.Wheels.Where(wheel => wheel.Height != 0 && (original || wheel.Polys is not null));
        var wheel = wheels.FirstOrDefault(wheel => wheel.Position.X >= 0);
        if (wheel.Height == 0) wheel = wheels.FirstOrDefault();
        if (wheel.Height == 0) wheel = source.Wheels[0];
        Request(key, new WheelMeshBuilder(wheel, source.Rims).BuildModel());
    }

    /// <summary>Drops a baked image so the next request renders it again (used after painting).</summary>
    public static void Invalidate(string id)
    {
        if (Cache.Remove(id, out var entry))
            Retired.Add((entry.Target, LoadingWork.FrameId + 1));

        Queued.Remove(id);
        Versions.Remove(id);
        if (Ready.TryGetValue(id, out var reference) && reference.TryGetTarget(out var signal))
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
        TrimUnused(visible);
        (string Id, Rad3d Rad, int Version, long ReadyAfterFrame) next = default;
        var found = false;
        for (var remaining = Pending.Count; remaining > 0; remaining--)
        {
            var pending = Pending.Dequeue();
            if (pending.Version != Versions.GetValueOrDefault(pending.Id)) continue;
            if (pending.ReadyAfterFrame > LoadingWork.FrameId)
            {
                Pending.Enqueue(pending);
                continue;
            }

            if (!visible(pending.Id))
            {
                Queued.Remove(pending.Id);
                Versions.Remove(pending.Id);
                continue;
            }

            next = pending;
            found = true;
            break;
        }

        if (!found) return;
        var (id, rad, _, _) = next;
        try
        {
            Render(cb, id, rad);
        }
        catch (Exception ex)
        {
            // A model that cannot be drawn only costs its card image.
            Logging.Warning($"Could not render a car thumbnail for '{id}': {ex.Message}");
        }
        finally
        {
            Queued.Remove(id);
            Versions.Remove(id);
        }
    }

    public static void TrimUnused(Func<string, bool>? visible = null, bool clear = false)
    {
        if (!clear && visible is null && Users.Count > 0) return;
        var now = Stopwatch.GetTimestamp();
        if (visible is not null)
            foreach (var pair in Cache)
                if (visible(pair.Key)) pair.Value.Used = now;
        var unused = Cache.Where(pair => visible is null || !visible(pair.Key))
            .OrderBy(pair => pair.Value.Used).ToArray();
        var excess = Math.Max(0, unused.Length - 32);
        foreach (var pair in unused)
        {
            if (!clear && excess <= 0 && Stopwatch.GetElapsedTime(pair.Value.Used, now) < TimeSpan.FromMinutes(2)) continue;
            Invalidate(pair.Key);
            excess--;
        }

        foreach (var pair in Ready.Where(pair => !pair.Value.TryGetTarget(out _)).ToArray()) Ready.Remove(pair.Key);
        if (clear || (visible is null && Users.Count == 0))
        {
            Pending.Clear();
            Queued.Clear();
            Versions.Clear();
            if (clear)
            {
                _generation++;
                LoadingModels.Clear();
                Users.Clear();
            }
        }

        if (clear) ReleaseRetired(force: true);
    }

    // Image nodes stop referencing retired textures when the UI scheduler flushes.
    public static void ReleaseRetired(bool force = false)
    {
        for (var index = Retired.Count - 1; index >= 0; index--)
        {
            if (!force && Retired[index].AfterFrame > LoadingWork.FrameId) continue;
            Retired[index].Target.Dispose();
            Retired.RemoveAt(index);
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

                var points = new List<Vector3>();
                AddMeshPoints(body, points);
                foreach (var wheel in wheels)
                    AddMeshPoints(wheel, points);
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

        if (Cache.Remove(id, out var previous)) Retired.Add((previous.Target, LoadingWork.FrameId + 1));
        Cache[id] = new(target, TheGraphics.G.WrapTexture(target.ColorTexture)!, rad);
        ReadySignal(id).Value = true;
    }

    private static void Downsample(ICommandBuffer cb, ITexture source, IRenderTarget target, int height)
    {
        var device = GameSparker.GraphicsDevice;
        // Store the image's top row at V=0 for UI sampling, including on GL.
        float[] vertices = [-1, -1, 0, 1, 3, -1, 2, 1, -1, 3, 0, -1];
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

    private static void AddMeshPoints(MeshedGameObject obj, List<Vector3> points)
    {
        var transform = obj.MatrixWorld;
        foreach (var poly in obj.Mesh.Polys)
        foreach (var point in poly.Points)
            points.Add(Vector3.Transform(point, transform));
    }

    private static PerspectiveCamera FitCamera(IReadOnlyList<Vector3> points, int height)
    {
        var center = points.Count > 0
            ? (points.Aggregate(Vector3.Min) + points.Aggregate(Vector3.Max)) * 0.5f
            : Vector3.Zero;
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

        var camera = new ThumbnailCamera(points)
        {
            Fov = Fov, Width = RenderWidth, Height = height * Supersampling, Near = NearPlane,
            PositionWithoutInterpolation = center + direction * distance,
            LookAtWithoutInterpolation = center,
            UpWithoutInterpolation = -Vector3.UnitY
        };
        camera.OnBeforeRender(1f);
        return camera;
    }

    private sealed class ThumbnailCamera(IReadOnlyList<Vector3> points) : PerspectiveCamera
    {
        public override void OnBeforeRender(float alpha)
        {
            base.OnBeforeRender(alpha);
            if (points.Count == 0) return;

            var minimum = new Vector2(float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity);
            foreach (var point in points)
            {
                var clip = Vector4.Transform(new Vector4(point, 1f), ViewProjectionMatrix);
                var projected = new Vector2(clip.X, clip.Y) / clip.W;
                minimum = Vector2.Min(minimum, projected);
                maximum = Vector2.Max(maximum, projected);
            }

            var midpoint = (minimum + maximum) * 0.5f;
            var halfSize = (maximum - minimum) * 0.5f;
            var extent = MathF.Max(halfSize.X, halfSize.Y);
            var zoom = extent > 0f ? FrameFill / extent : 1f;

            // Shift and scale the projected silhouette while keeping the perspective and clip depths.
            ProjectionMatrix *= Matrix.CreateTranslation(-midpoint.X, -midpoint.Y, 0f)
                                * Matrix.CreateScale(zoom, zoom, 1f);
            ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        }
    }
}
