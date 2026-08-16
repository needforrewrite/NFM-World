using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Util;
using Environment = NFMWorld.Environment;
using NFMWorld.Sentry;

namespace NFMWorld;

/**
Represents a stage. Holds all information relating to track pices, scenery, etc.
But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public class ClientStageRenderer : GameObject, IDisposable
{
    private GraphicsDevice _graphicsDevice;
    private bool _disposed;

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

    // we use the object instance hashcode instead of the value hashcode for performance
    private class StageObjectVisualComparer : IEqualityComparer<StageObject>
    {
        public static StageObjectVisualComparer Instance { get; } = new();
        
        public bool Equals(StageObject? x, StageObject? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return ReferenceEquals(x.Rad, y.Rad) && x.Position.Equals(y.Position) && x.Rotation.Equals(y.Rotation) && x.IsSpecial == y.IsSpecial && x.Kind == y.Kind;
        }

        public int GetHashCode(StageObject obj)
        {
            return HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Rad), obj.Position, obj.Rotation, obj.IsSpecial, obj.Kind);
        }
    }

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public ClientStageRenderer(GraphicsDevice graphicsDevice, BackendStage backendStage)
    {
        _graphicsDevice = graphicsDevice;
        this.backendStage = backendStage;
        Children = _mutableChildren;
        World.ResetValues();
        try
        {
            var stageLoader = backendStage.StageLoader;

            ApplyValues();

            if (stageLoader.DrawPolys)
            {
                polys?.Dispose();
                polys = Environment.MakePolys(backendStage, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb, stageLoader.maxt - stageLoader.maxb, backendStage.StagePartCount, graphicsDevice);
                _polysKey = (stageLoader.DrawPolys, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb, stageLoader.maxt - stageLoader.maxb, backendStage.StagePartCount);
            }
            else
            {
                _polysKey = (false, 0, 0, 0, 0, 0);
            }

            if (stageLoader.DrawClouds)
            {
                clouds?.Dispose();
                clouds = Environment.MakeClouds(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt, graphicsDevice);
                _cloudsKey = (stageLoader.DrawClouds, stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
            }
            else
            {
                _cloudsKey = (false, 0, 0, 0, 0);
            }

            if (stageLoader.DrawMountains)
            {
                mountains?.Dispose();
                mountains = Environment.MakeMountains(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt, graphicsDevice);
                _mountainsKey = (stageLoader.DrawMountains, stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt);
            }
            else
            {
                _mountainsKey = (false, 0, 0, 0, 0);
            }
            
            foreach (var piece in backendStage.Pieces)
            {
                if (piece is StageObject obj)
                {
                    if (_cachedObjects.ContainsKey(obj)) continue;
                    
                    var mesh = GameSparker.GetStagePartMesh(obj.Rad);
                    if (obj.Kind == AiNodeKind.CheckPoint)
                    {
                        var clientObj = new StageObjectGameObject(mesh, obj)
                        {
                            Parent = this
                        };
                        _mutableChildren.Add(clientObj);
                        
                        _cachedObjects[obj] = clientObj;
                    }
                    else if (obj.Kind == AiNodeKind.FixHoop)
                    {
                        var clientObj = new FixHoop(mesh, obj)
                        {
                            Parent = this
                        };
                        _mutableChildren.Add(clientObj);
                        
                        _cachedObjects[obj] = clientObj;
                    }
                    else
                    {
                        var clientObj = new StageObjectGameObject(mesh, obj)
                        {
                            Parent = this
                        };
                        _mutableChildren.Add(clientObj);
                        
                        _cachedObjects[obj] = clientObj;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            SentrySdk.CaptureException(exception);
            Logging.Error($"Error in stage: {backendStage.Name}");
            Logging.Error(exception.ToString());
        }
        sky = new Sky(graphicsDevice);
        ground = new Ground(graphicsDevice);
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
            polysKey = (stageLoader.DrawPolys, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb, stageLoader.maxt - stageLoader.maxb, backendStage.StagePartCount);
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
            polys = Environment.MakePolys(backendStage, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl, stageLoader.maxb, stageLoader.maxt - stageLoader.maxb, backendStage.StagePartCount, _graphicsDevice);
            _polysKey = polysKey;
        }

        if (_cloudsKey != cloudsKey)
        {
            clouds = Environment.MakeClouds(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt, _graphicsDevice);
            _cloudsKey = cloudsKey;
        }

        if (_mountainsKey != mountainsKey)
        {
            mountains = Environment.MakeMountains(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb, stageLoader.maxt, _graphicsDevice);
            _mountainsKey = mountainsKey;
        }

        foreach (var piece in backendStage.Pieces)
        {
            if (piece is StageObject obj)
            {
                seenObjects.Add(obj);
                if (_cachedObjects.ContainsKey(obj)) continue;
                
                var mesh = GameSparker.GetStagePartMesh(obj.Rad);
                if (obj.Kind == AiNodeKind.CheckPoint)
                {
                    var clientObj = new StageObjectGameObject(mesh, obj)
                    {
                        Parent = this
                    };
                    _mutableChildren.Add(clientObj);

                    _cachedObjects[obj] = clientObj;
                }
                else if (obj.Kind == AiNodeKind.FixHoop)
                {
                    var clientObj = new FixHoop(mesh, obj)
                    {
                        Parent = this
                    };
                    _mutableChildren.Add(clientObj);

                    _cachedObjects[obj] = clientObj;
                }
                else
                {
                    var clientObj = new StageObjectGameObject(mesh, obj)
                    {
                        Parent = this
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
                objsToRemove.Add(obj);
            }
        }

        foreach (var obj in objsToRemove)
        {
            _cachedObjects.Remove(obj);
        }
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
    }

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        sky?.SubmitDraws(queue, camera, lighting, pass);
        ground?.SubmitDraws(queue, camera, lighting, pass);
        polys?.SubmitDraws(queue, camera, lighting, pass);
        clouds?.SubmitDraws(queue, camera, lighting, pass);
        mountains?.SubmitDraws(queue, camera, lighting, pass);

        base.SubmitDraws(queue, camera, lighting, pass);
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
        var checkpointStageObject = backendStage.Checkpoints[currentCheckpoint];
        
        ResetCheckpointGlow();

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

        // Null out environment references so their finalizers can release GPU resources.
        // These objects (Sky, Ground, GroundPolys, Mountains) only have finalizers,
        // not public Dispose methods. Letting them go out of scope allows GC to collect them.
        sky?.Dispose();
        ground?.Dispose();
        polys?.Dispose();
        clouds?.Dispose();
        mountains?.Dispose();

        _cachedObjects.Clear();
        _mutableChildren.Clear();

        GC.SuppressFinalize(this);
    }

    #endregion
}