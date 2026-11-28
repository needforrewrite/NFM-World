using System.Runtime.CompilerServices;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorld.Sentry;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Rendering;
using NFMWorldLibrary.Util;
using NFMWorldLibrary.FixedMath;

namespace NFMWorld;

/**
Represents a stage. Holds all information relating to track pices, scenery, etc.
But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public partial class ClientStageRenderer : GameObject, IDisposable
{
    private bool _disposed;
    private int _environmentSeed;
    private IDisposable? _pendingOwnership;

    internal void Adopt()
    {
        _pendingOwnership?.Dispose();
        _pendingOwnership = null;
    }

    public Sky? sky;
    public Ground? ground;
    public GroundPolys? polys;
    public GroundPolys? clouds;
    public Mountains? mountains;

    private readonly BackendStage backendStage;

    private readonly Dictionary<StageObject, StageObjectGameObject> _cachedObjects = new(StageObjectVisualComparer.Instance);
    private List<GameObject> _mutableChildren = [];

    private (bool drawPolys, int sx, int ncx, int sz, int ncz, int stagePartCount) _polysKey;
    private (bool drawClouds, int maxl, int maxr, int maxb, int maxt) _cloudsKey;
    private (bool drawMountains, int maxl, int maxr, int maxb, int maxt) _mountainsKey;

    private int _lastIndex;
    private readonly List<Mesh> _resolvedMeshes = [];
    private readonly Dictionary<Rad3d, ResourceLease<Mesh>> _sharedMeshes = new(Rad3d.VisualEqualityComparer.Instance);

    private Mesh GetStageMesh(Rad3d model, Mesh.Prepared? prepared = null)
    {
        if (!_sharedMeshes.TryGetValue(model, out var lease))
            _sharedMeshes.Add(model, lease = StageMeshCache.Acquire(model, prepared));

        return lease.Value;
    }

    // we use the object instance hashcode instead of the value hashcode for performance
    private class StageObjectVisualComparer : IEqualityComparer<StageObject>
    {
        public static StageObjectVisualComparer Instance { get; } = new();

        public bool Equals(StageObject? x, StageObject? y) => ReferenceEquals(x, y);

        // One visual per stage object. Value equality would let a pasted duplicate share (or
        // suppress) the original's visual, since the renderer keys its cache by this comparer.
        public int GetHashCode(StageObject obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public ClientStageRenderer(BackendStage backendStage)
    {
        this.backendStage = backendStage;
        InitializeDestructionRendering();
        Children = _mutableChildren;
        World.ResetValues();
        _environmentSeed = World.MountainSeed;

        try
        {
            var stageLoader = backendStage.StageLoader;

            ApplyValues();

            if (stageLoader.DrawPolys)
            {
                polys?.Dispose();
                polys = Environment.MakePolys(
                    backendStage,
                    stageLoader.maxl,
                    stageLoader.maxr - stageLoader.maxl,
                    stageLoader.maxb,
                    stageLoader.maxt - stageLoader.maxb,
                    backendStage.StagePartCount
                );
                _polysKey = (stageLoader.DrawPolys, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb,
                    stageLoader.maxt - stageLoader.maxb, backendStage.StagePartCount);
            }
            else
            {
                _polysKey = (false, 0, 0, 0, 0, 0);
            }

            if (stageLoader.DrawClouds)
            {
                clouds?.Dispose();
                clouds = Environment.MakeClouds(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
                _cloudsKey = (stageLoader.DrawClouds, stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
            }
            else
            {
                _cloudsKey = (false, 0, 0, 0, 0);
            }

            if (stageLoader.DrawMountains)
            {
                mountains?.Dispose();
                mountains = Environment.MakeMountains(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
                _mountainsKey = (stageLoader.DrawMountains, stageLoader.maxl, stageLoader.maxr, stageLoader.maxb,
                    stageLoader.maxt);
            }
            else
            {
                _mountainsKey = (false, 0, 0, 0, 0);
            }

            foreach (var piece in backendStage.Pieces)
            {
                if (piece is StageObject obj)
                {
                    if (_cachedObjects.ContainsKey(obj))

                        continue;

                    var mesh = GetStageMesh(obj.Rad);

                    if (obj.Kind == AiNodeKind.CheckPoint)
                    {
                        var clientObj = new StageObjectGameObject(mesh, obj)
                        {
                            Parent = this,
                            Layer = _lastIndex++
                        };
                        _mutableChildren.Add(clientObj);

                        _cachedObjects[obj] = clientObj;
                    }
                    else if (obj.Kind == AiNodeKind.FixHoop)
                    {
                        var clientObj = new FixHoop(mesh, obj)
                        {
                            Parent = this,
                            Layer = _lastIndex++
                        };
                        _mutableChildren.Add(clientObj);

                        _cachedObjects[obj] = clientObj;
                    }
                    else
                    {
                        var clientObj = new StageObjectGameObject(mesh, obj)
                        {
                            Parent = this,
                            Layer = _lastIndex++
                        };
                        _mutableChildren.Add(clientObj);

                        _cachedObjects[obj] = clientObj;
                    }
                }
            }

            ResolveSurfaceOverlaps();
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {backendStage.Name}");
            Logging.Error(exception.ToString());
        }

        sky = new Sky(GameSparker.GraphicsDevice);
        ground = new Ground(GameSparker.GraphicsDevice);
    }

    private ClientStageRenderer(BackendStage stage, bool deferred)
    {
        backendStage = stage;
        InitializeDestructionRendering();
        Children = _mutableChildren;
    }

    private Environment.GenerationSettings CaptureGenerationSettings()
    {
        var saved = (World.HasTexture, World.HasClouds, World.HasPolys, World.CloudCoverage,
            World.MountainCoverage, World.LightsOn, World.DrawClouds, World.DrawMountains, World.DrawStars,
            World.DrawPolys, World.MountainSeed, World.FogDensity, World.LightDirection, World.Snap, World.Fog,
            World.Sky, World.GroundColor, World.GroundPolysColor, World.FadeFrom, World.Clouds, World.CloudColor,
            World.Texture);

        try
        {
            World.ResetValues();
            ApplyValues();

            return Environment.GenerationSettings.Capture();
        }
        finally
        {
            (World.HasTexture, World.HasClouds, World.HasPolys, World.CloudCoverage,
                World.MountainCoverage, World.LightsOn, World.DrawClouds, World.DrawMountains, World.DrawStars,
                World.DrawPolys, World.MountainSeed, World.FogDensity, World.LightDirection, World.Snap, World.Fog,
                World.Sky, World.GroundColor, World.GroundPolysColor, World.FadeFrom, World.Clouds, World.CloudColor,
                World.Texture) = saved;
        }
    }

    internal static async Task<ClientStageRenderer> CreateAsync(
        BackendStage stage,
        CancellationToken token,
        bool generateEnvironment = true
    )
    {
        await LoadingWork.NextFrame;
        token.ThrowIfCancellationRequested();
        var renderer = new ClientStageRenderer(stage, deferred: true);
        renderer._pendingOwnership = ContentCacheMaintenance.TrackPending(renderer);

        try
        {
            var settings = renderer.CaptureGenerationSettings();
            renderer._environmentSeed = settings.MountainSeed;
            var loader = stage.StageLoader;
            var objects = stage.Pieces.OfType<StageObject>().Select(obj => (Object: obj, Polys: obj.Rad.Polys,
                World: Matrix.CreateFromEuler((Euler)obj.Rotation) * Matrix.CreateTranslation((Vector3)obj.Position))).ToArray();
            foreach (var entry in objects)
                if (!renderer._sharedMeshes.ContainsKey(entry.Object.Rad)
                    && StageMeshCache.TryAcquire(entry.Object.Rad, out var lease))
                    renderer._sharedMeshes.Add(entry.Object.Rad, lease!);
            var cachedModels = new HashSet<Rad3d>(renderer._sharedMeshes.Keys, Rad3d.VisualEqualityComparer.Instance);
            var prepared = await Task.Run(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    var groundPolys = generateEnvironment && loader.DrawPolys ? EnvironmentMeshData.Prepare(
                        Environment.BuildPolys(
                            stage,
                            loader.maxl,
                            loader.maxr - loader.maxl,
                            loader.maxb,
                            loader.maxt - loader.maxb,
                            stage.StagePartCount,
                            settings
                        )
                    ) : null;
                    token.ThrowIfCancellationRequested();
                    var clouds = generateEnvironment && loader.DrawClouds
                    ? EnvironmentMeshData.Prepare(Environment.BuildClouds(
                        loader.maxl,
                        loader.maxr,
                        loader.maxb,
                        loader.maxt,
                        settings
                    )
                    ) : null;
                    token.ThrowIfCancellationRequested();
                    var mountains = generateEnvironment && loader.DrawMountains
                    ? EnvironmentMeshData.Prepare(Environment.BuildMountains(
                        loader.maxl,
                        loader.maxr,
                        loader.maxb,
                        loader.maxt,
                        settings
                    )
                    ) : null;
                    var resolver = new CoplanarSurfaceResolver();
                    var pieces = new List<(StageObject Object, Mesh.Prepared? Replacement)>();
                    var meshes = new Dictionary<Rad3d, Mesh.Prepared>(Rad3d.VisualEqualityComparer.Instance);

                    foreach (var entry in objects)
                    {
                        token.ThrowIfCancellationRequested();
                        var rad = entry.Object.Rad;
                        if (!cachedModels.Contains(rad) && !meshes.ContainsKey(rad))
                            meshes.Add(rad, Mesh.Prepare(rad));
                        var polygons = entry.Object.Durability > 0 ? entry.Polys
                            : StageSurfaceOverlaps.Resolve(resolver, pieces.Count, entry.Polys, entry.World);
                        var replacement = ReferenceEquals(polygons, entry.Polys) ? null
                        : Mesh.Prepare(new Rad3d(polygons, rad.CastsShadow, rad.FileName));
                        pieces.Add((entry.Object, replacement));
                    }

                    return (groundPolys, clouds, mountains, pieces, meshes);
                },
                token
            );

            await LoadingWork.NextFrame;
            token.ThrowIfCancellationRequested();
            if (prepared.groundPolys is { } polys)
                renderer.polys = new GroundPolys(GameSparker.GraphicsDevice, polys);
            await LoadingWork.NextFrame;
            token.ThrowIfCancellationRequested();
            if (prepared.clouds is { } clouds)
                renderer.clouds = new GroundPolys(GameSparker.GraphicsDevice, clouds, applyFog: false);
            await LoadingWork.NextFrame;
            token.ThrowIfCancellationRequested();
            if (prepared.mountains is { } mountains)
                renderer.mountains = new Mountains(GameSparker.GraphicsDevice, mountains);
            var uploadTimer = System.Diagnostics.Stopwatch.StartNew();

            foreach (var (obj, replacement) in prepared.pieces)
            {
                if (uploadTimer.ElapsedMilliseconds >= 2)
                {
                    await LoadingWork.NextFrame;
                    uploadTimer.Restart();
                }

                token.ThrowIfCancellationRequested();
                var mesh = renderer.GetStageMesh(obj.Rad, prepared.meshes.GetValueOrDefault(obj.Rad));

                if (replacement is not null)
                {
                    mesh = new Mesh(replacement);
                    renderer._resolvedMeshes.Add(mesh);
                }

                StageObjectGameObject visual = obj.Kind == AiNodeKind.FixHoop
                    ? new FixHoop(mesh, obj)
                    : new StageObjectGameObject(mesh, obj);
                visual.Parent = renderer;
                visual.Layer = renderer._lastIndex++;
                renderer._mutableChildren.Add(visual);
                renderer._cachedObjects.Add(obj, visual);
            }

            await LoadingWork.NextFrame;
            token.ThrowIfCancellationRequested();
            renderer.sky = new Sky(
                GameSparker.GraphicsDevice,
                settings.Sky.Snap(settings.Snap),
                settings.Fog.Snap(settings
                    .Snap
            )
            );
            renderer.ground = new Ground(
                GameSparker.GraphicsDevice,
                settings.Ground,
                (Color)settings.GroundColor.Snap(settings.Snap)
            );
            renderer._polysKey = loader.DrawPolys ? (true, loader.maxl, loader.maxr - loader.maxl,
                loader.maxb, loader.maxt - loader.maxb, stage.StagePartCount) : (false, 0, 0, 0, 0, 0);
            renderer._cloudsKey = loader.DrawClouds ? (true, loader.maxl, loader.maxr, loader.maxb, loader.maxt) : (false, 0, 0,
                0, 0);
            renderer._mountainsKey = loader.DrawMountains ? (true, loader.maxl, loader.maxr, loader.maxb, loader.maxt) : (false,
                0, 0, 0, 0);

            return renderer;
        }
        catch
        {
            await LoadingWork.NextFrame;
            renderer.Dispose();

            throw;
        }
    }

    public void DetectChanges(bool updateEnvironment = false)
    {
        var seenObjects = new List<StageObject>();

        var stageLoader = backendStage.StageLoader;

        (bool drawPolys, int sx, int ncx, int sz, int ncz, int stagePartCount) polysKey;
        (bool drawClouds, int maxl, int maxr, int maxb, int maxt) cloudsKey;
        (bool drawMountains, int maxl, int maxr, int maxb, int maxt) mountainsKey;

        if (stageLoader.DrawPolys)
        {
            polysKey = (stageLoader.DrawPolys, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb,
                stageLoader.maxt - stageLoader.maxb, backendStage.StagePartCount);
        }
        else
        {
            polysKey = (false, 0, 0, 0, 0, 0);
        }

        if (stageLoader.DrawClouds)
        {
            cloudsKey = (stageLoader.DrawClouds, stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
        }
        else
        {
            cloudsKey = (false, 0, 0, 0, 0);
        }

        if (stageLoader.DrawMountains)
        {
            mountainsKey = (stageLoader.DrawMountains, stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
        }
        else
        {
            mountainsKey = (false, 0, 0, 0, 0);
        }

        if (_polysKey != polysKey)
        {
            var nextPolys = stageLoader.DrawPolys ? Environment.MakePolys(
                backendStage,
                stageLoader.maxl,
                stageLoader.maxr - stageLoader.maxl,
                stageLoader.maxb,
                stageLoader.maxt - stageLoader.maxb,
                backendStage.StagePartCount
            ) : null;
            polys?.Dispose();
            polys = nextPolys;
            _polysKey = polysKey;
        }

        if (_cloudsKey != cloudsKey)
        {
            var nextClouds = stageLoader.DrawClouds
                ? Environment.MakeClouds(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt) : null;
            clouds?.Dispose();
            clouds = nextClouds;
            _cloudsKey = cloudsKey;
        }

        if (_mountainsKey != mountainsKey)
        {
            var nextMountains = stageLoader.DrawMountains
                ? Environment.MakeMountains(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt) : null;
            mountains?.Dispose();
            mountains = nextMountains;
            _mountainsKey = mountainsKey;
        }

        var geometryChanged = false;

        foreach (var piece in backendStage.Pieces)
        {
            if (piece is StageObject obj)
            {
                seenObjects.Add(obj);

                if (_cachedObjects.ContainsKey(obj))

                    continue;
                geometryChanged = true;

                var mesh = GetStageMesh(obj.Rad);

                if (obj.Kind == AiNodeKind.CheckPoint)
                {
                    var clientObj = new StageObjectGameObject(mesh, obj)
                    {
                        Parent = this,
                        Layer = _lastIndex++,
                    };
                    _mutableChildren.Add(clientObj);

                    _cachedObjects[obj] = clientObj;
                }
                else if (obj.Kind == AiNodeKind.FixHoop)
                {
                    var clientObj = new FixHoop(mesh, obj)
                    {
                        Parent = this,
                        Layer = _lastIndex++,
                    };
                    _mutableChildren.Add(clientObj);

                    _cachedObjects[obj] = clientObj;
                }
                else
                {
                    var clientObj = new StageObjectGameObject(mesh, obj)
                    {
                        Parent = this,
                        Layer = _lastIndex++,
                    };
                    _mutableChildren.Add(clientObj);

                    _cachedObjects[obj] = clientObj;
                }
            }
        }

        var objsToRemove = new List<StageObject>();

        foreach (var (obj, clientObj) in _cachedObjects)
        {
            if (!seenObjects.Contains(obj))
            {
                _mutableChildren.Remove(clientObj);
                clientObj.Dispose();
                objsToRemove.Add(obj);
            }
        }

        foreach (var obj in objsToRemove)
        {
            _cachedObjects.Remove(obj);
        }

        if (geometryChanged || objsToRemove.Count > 0)
            ResolveSurfaceOverlaps();
        var neededModels = new HashSet<Rad3d>(_cachedObjects.Keys.Select(obj => obj.Rad), Rad3d.VisualEqualityComparer.Instance);

        foreach (var model in _sharedMeshes.Keys.Where(model => !neededModels.Contains(model)).ToArray())
        {
            _sharedMeshes[model].Dispose();
            _sharedMeshes.Remove(model);
        }
    }

    private void ResolveSurfaceOverlaps()
    {
        var resolver = new CoplanarSurfaceResolver();
        var entries = _cachedObjects.OrderBy(entry => entry.Value.Layer ?? 0).ToArray();
        var replacements = new List<(StageObjectGameObject Visual, Mesh Mesh)>();
        var owned = new List<Mesh>();

        try
        {
            for (var i = 0; i < entries.Length; i++)
            {
                var (obj, visual) = entries[i];
                var world = Matrix.CreateFromEuler((Euler)obj.Rotation) * Matrix.CreateTranslation((Vector3)obj.Position);
                var polygons = obj.Durability > 0 ? obj.Rad.Polys
                    : StageSurfaceOverlaps.Resolve(resolver, i, obj.Rad.Polys, world);
                var mesh = GetStageMesh(obj.Rad);

                if (!ReferenceEquals(polygons, obj.Rad.Polys))
                {
                    mesh = new Mesh(new Rad3d(polygons, obj.Rad.CastsShadow, obj.Rad.FileName));
                    owned.Add(mesh);
                }

                replacements.Add((visual, mesh));
            }
        }
        catch
        {
            foreach (var mesh in owned)
                mesh.Dispose();

            throw;
        }

        foreach (var (visual, mesh) in replacements)
            visual.Mesh = mesh;
        foreach (var mesh in _resolvedMeshes)
            mesh.Dispose();
        _resolvedMeshes.Clear();
        _resolvedMeshes.AddRange(owned);
    }

    public void ApplyValues()
    {
        foreach (var instruction in backendStage.StageLoader.EnvironmentInstructions)
        {
            switch (instruction)
            {
                case CloudsInstruction clouds:
                    World.HasClouds = true;

                    break;
                case FogInstruction fog:
                    World.Fog = fog.Color;

                    break;
                case GroundInstruction ground:
                    World.SetGround(ground.Color);

                    break;
                case PolysInstruction polys:
                    World.GroundPolysColor = polys.Color;
                    World.HasPolys = true;

                    break;
                case SkyInstruction sky:
                    World.SetSky(sky.Color);

                    break;
                case SnapInstruction snap:
                    World.Snap = snap.Color;

                    break;
                case TextureInstruction texture:
                    World.SetTexture(texture.Texture);
                    World.HasTexture = true;

                    break;
                default:

                    throw new ArgumentOutOfRangeException(nameof(instruction), instruction, null);
            }
        }

        World.DrawPolys = backendStage.StageLoader.DrawPolys;
        World.HasPolys = backendStage.StageLoader.DrawPolys && World.HasPolys;

        World.DrawClouds = backendStage.StageLoader.DrawClouds;
        World.HasClouds = backendStage.StageLoader.DrawClouds && World.HasClouds;

        if (backendStage.StageLoader.CloudCoverage is { } cloudCoverage)
        {
            World.CloudCoverage = cloudCoverage;
        }

        if (backendStage.StageLoader.FogDensity is { } fogDensity)
        {
            World.FogDensity = fogDensity;
        }

        if (backendStage.StageLoader.FadeFrom is { } fadeFrom)
        {
            World.FadeFrom = fadeFrom;
        }

        if (backendStage.StageLoader.LightsOn)
        {
            World.LightsOn = true;
        }

        World.DrawMountains = backendStage.StageLoader.DrawMountains;
        World.DrawStars = backendStage.StageLoader.DrawStars;

        if (backendStage.StageLoader.MountainSeed is { } mountainSeed)
        {
            World.MountainSeed = mountainSeed;
        }

        if (backendStage.StageLoader.MountainCoverage is { } mountainCoverage)
        {
            World.MountainCoverage = mountainCoverage;
        }

        if (backendStage.StageLoader.LightDirection is { } lightDirection)
        {
            World.LightDirection = lightDirection;
        }

        if (backendStage.StageLoader.FlameColors is { } flames)
        {
            World.FlameOuter = flames.Outer;
            World.FlameInner = flames.Inner;
            World.FlameEmber = flames.Ember;
            World.FlameSnapDiv = new Vector3(flames.SnapDivX, flames.SnapDivY, flames.SnapDivZ);
        }

        // pulsegrnd scales the colours the ground and its polygons were built with, so keep the
        // base pair and tint the draw rather than rebuilding any mesh.
        _pulse = backendStage.StageLoader.Pulse;
        _pulseGround = World.GroundColor;
        _pulsePolys = World.GroundPolysColor;
        _pulseTicks = 0;
        _pulsePhase = 0f;
        World.GroundPulseTint = Vector3.One;
    }

    private GroundPulse? _pulse;
    private Color3 _pulseGround;
    private Color3 _pulsePolys;
    private int _pulseTicks;
    private float _pulsePhase;

    /// <summary>
    /// Advances <c>pulsegrnd</c> at the original game's rate: phy stepped its ground pulse once
    /// per 21.4 Hz tick, not once per frame.
    /// </summary>
    private void TickGroundPulse()
    {
        if (_pulse is not { } pulse)

            return;
        if (++_pulseTicks < Physics.OriginalTicksPerNewTick)

            return;
        _pulseTicks = 0;

        var t = pulse.A + pulse.B * MathF.Sin(_pulsePhase * pulse.C);
        _pulsePhase++;
        World.GroundPulseTint = new Vector3(t, t, t);
        World.GroundColor = Scale(_pulseGround, t);

        // Ground polygons are baked, so their colour only reaches the shader through the tint;
        // this pair keeps the CGround polys (which read World.GroundColor) in step.
        World.GroundPolysColor = Scale(_pulsePolys, t);
    }

    /// <summary>
    /// Re-reads the un-pulsed ground colours. The editor changes them live without re-running
    /// <see cref="ApplyValues"/>, and the pulse must scale the new pair, not the old one.
    /// </summary>
    public void RefreshGroundPulseBase()
    {
        _pulseGround = World.GroundColor;
        _pulsePolys = World.GroundPolysColor;
    }

    private static Color3 Scale(Color3 color, float factor)
        => new(
            (short)Math.Clamp((int)(color.R * factor), 0, 255),
            (short)Math.Clamp((int)(color.G * factor), 0, 255),
            (short)Math.Clamp((int)(color.B * factor), 0, 255)
        );

    public void Activate()
    {
        World.ResetValues();
        World.MountainSeed = _environmentSeed;
        ApplyValues();
    }

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        sky?.SubmitDraws(queue, camera, lighting, pass);
        ground?.SubmitDraws(queue, camera, lighting, pass);
        polys?.SubmitDraws(queue, camera, lighting, pass);
        clouds?.SubmitDraws(queue, camera, lighting, pass);
        mountains?.SubmitDraws(queue, camera, lighting, pass);

        base.SubmitDraws(queue, camera, lighting, pass);
        foreach (var debris in _debris)
            debris.SubmitDraws(queue, camera, lighting, pass);
    }

    public void ResetCheckpointGlow()
    {
        foreach (var checkpoint in _mutableChildren)
        {
            if (checkpoint is StageObjectGameObject stageObjectGameObject)
            {
                stageObjectGameObject.Glow = false;
                stageObjectGameObject.Finish = false;
            }
        }
    }

    public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
    {
        ResetCheckpointGlow();

        // A stage can declare laps and no checkpoint gates at all (football/stadium does), and the
        // array hands back default past its end - a null key is not a dictionary lookup here.

        if (currentCheckpoint >= backendStage.Checkpoints.Count)

            return;
        if (backendStage.Checkpoints[currentCheckpoint] is not { } checkpointStageObject)

            return;

        if (_cachedObjects.TryGetValue(checkpointStageObject, out var gameObject))
        {
            if (isFinish)
            {
                gameObject.Finish = true;
            }

            gameObject.Glow = true;
        }
    }

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)

            return;

        _disposed = true;
        DisposeDestructionRendering();
        Adopt();

        sky?.Dispose();
        ground?.Dispose();
        polys?.Dispose();
        clouds?.Dispose();
        mountains?.Dispose();

        foreach (var mesh in _resolvedMeshes)
            mesh.Dispose();
        _resolvedMeshes.Clear();

        foreach (var visual in _cachedObjects.Values)
            visual.Dispose();
        _cachedObjects.Clear();
        _mutableChildren.Clear();
        foreach (var lease in _sharedMeshes.Values)
            lease.Dispose();
        _sharedMeshes.Clear();

        GC.SuppressFinalize(this);
    }

    #endregion
}
