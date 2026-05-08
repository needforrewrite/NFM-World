using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Mad;
using NFMWorldLibrary.Util;
using Environment = NFMWorld.Environment;

namespace NFMWorld;

/**
Represents a stage. Holds all information relating to track pices, scenery, etc.
But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public class ClientStageRenderer : GameObject
{
    public UnlimitedArray<StageObjectGameObject> checkpoints = [];
    public UnlimitedArray<StageObjectGameObject> fixhoops = [];
    
    public Sky? sky;
    public Ground? ground;
    public GroundPolys? polys;
    public GroundPolys? clouds;
    public Mountains? mountains;

    private int? _fadeFrom = null;

    // soundtrack(folder,fileName)
    public string musicPath = "";
    // soundtrackremaster(folder,fileName)
    public string remasteredMusicPath = "";
    // soundtrackfreqmul(mul)
    public double musicFreqMul = 1.0d;
    public double musicTempoMul = 0d;

    public void ReapplyFadeFrom()
    {
        if(_fadeFrom != null)
            World.FadeFrom = (int)_fadeFrom;
    }

    /**
 * Loads stage currently set by checkpoints.stage onto stageContos
 */
    public ClientStageRenderer(GraphicsDevice graphicsDevice, BackendStage backendStage)
    {
        var children = new List<GameObject>();
        Children = children;
        World.ResetValues();
        try
        {
            var stageLoader = backendStage.stageLoader;

            if (stageLoader.Snap is { } snap)
            {
                World.Snap = snap;
            }

            if (stageLoader.Sky is { } sky)
            {
                World.Sky = sky;
            }

            if (stageLoader.GroundColor is { } ground)
            {
                World.GroundColor = ground;
            }

            World.DrawPolys = stageLoader.DrawPolys;
            World.HasPolys = stageLoader.DrawPolys && stageLoader.GroundPolysColor is not null;
            if (stageLoader.GroundPolysColor is { } groundPolys)
            {
                World.GroundPolysColor = groundPolys;
            }

            if (stageLoader.Fog is { } fog)
            {
                World.Fog = fog;
            }

            if (stageLoader.Texture is { } texture)
            {
                World.HasTexture = true;
                World.Texture = [..texture];
            }

            World.DrawClouds = stageLoader.DrawClouds;
            World.HasClouds = stageLoader.DrawClouds && stageLoader.Clouds is not null;
            if (stageLoader.Clouds is { } aclouds)
            {
                World.Clouds = [..aclouds];
            }

            if (stageLoader.CloudCoverage is { } cloudCoverage)
            {
                World.CloudCoverage = cloudCoverage;
            }

            if (stageLoader.FogDensity is { } fogDensity)
            {
                World.FogDensity = fogDensity;
            }

            if (stageLoader.FadeFrom is { } fadeFrom)
            {
                World.FadeFrom = fadeFrom;
                _fadeFrom = World.FadeFrom;
            }

            if (stageLoader.LightsOn)
            {
                World.LightsOn = true;
            }

            World.DrawMountains = stageLoader.DrawMountains;
            if (stageLoader.MountainSeed is { } mountainSeed)
            {
                World.MountainSeed = mountainSeed;
            }

            if (stageLoader.MountainCoverage is { } mountainCoverage)
            {
                World.MountainCoverage = mountainCoverage;
            }

            if (stageLoader.LightDirection is { } lightDirection)
            {
                World.LightDirection = lightDirection;
            }

            musicPath = stageLoader.musicPath;
            remasteredMusicPath = stageLoader.remasteredMusicPath;
            musicFreqMul = stageLoader.musicFreqMul;
            musicTempoMul = stageLoader.musicTempoMul;

            if (string.IsNullOrEmpty(musicPath))
            {
                Logging.Error("No music is defined for this stage!");
            }

            // Medium.Newpolys(maxl, maxr - maxl, maxb, maxt - maxb, stagePartCount);
            // Medium.Newmountains(maxl, maxr, maxb, maxt);
            // Medium.Newclouds(maxl, maxr, maxb, maxt);
            // Medium.Newstars();
            if (World.DrawPolys)
            {
                polys = Environment.MakePolys(backendStage, stageLoader.maxl, stageLoader.maxr - stageLoader.maxl,
                    stageLoader.maxb, stageLoader.maxt - stageLoader.maxb, backendStage.stagePartCount, graphicsDevice);
            }

            if (World.DrawClouds)
            {
                clouds = Environment.MakeClouds(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb,
                    stageLoader.maxt, graphicsDevice);
            }

            if (World.DrawMountains)
            {
                mountains = Environment.MakeMountains(stageLoader.maxl, stageLoader.maxr, stageLoader.maxb,
                    stageLoader.maxt, graphicsDevice);
            }
            
            foreach (var piece in backendStage.pieces)
            {
                if (piece is StageObject obj)
                {
                    var mesh = GameSparker.GetStagePartMesh(obj.Rad);
                    if (obj.Kind == AiNodeKind.CheckPoint)
                    {
                        var clientObj = new StageObjectGameObject(mesh, obj)
                        {
                            Parent = this
                        };
                        children.Add(clientObj);

                        checkpoints.Add(clientObj);
                    }
                    else if (obj.Kind == AiNodeKind.FixHoop)
                    {
                        var clientObj = new FixHoop(mesh, obj)
                        {
                            Parent = this
                        };
                        children.Add(clientObj);

                        fixhoops.Add(clientObj);
                    }
                    else
                    {
                        var clientObj = new StageObjectGameObject(mesh, obj)
                        {
                            Parent = this
                        };
                        children.Add(clientObj);
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

    public override void Render(Camera.Camera camera, Lighting? lighting)
    {
        sky?.Render(camera, lighting);
        ground?.Render(camera, lighting);
        polys?.Render(camera, lighting);
        clouds?.Render(camera, lighting);
        mountains?.Render(camera, lighting);
        base.Render(camera, lighting);
    }

    public void ResetCheckpointGlow()
    {
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.Glow = false;
            checkpoint.Finish = false;
        }
    }

    public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
    {
        if (checkpoints.Count > 0)
        {
            if (isFinish)
            {
                checkpoints[^1].Finish = true;
            }
            else
            {
                checkpoints[^1].Finish = false;
            }

            if (currentCheckpoint > 0)
            {
                checkpoints[currentCheckpoint - 1].Glow = false;
            }
            else
            {
                checkpoints[^1].Glow = false;
            }

            if (currentCheckpoint < checkpoints.Count)
            {
                checkpoints[currentCheckpoint].Glow = true;
            }
        }
    }
}