using NFMWorldLibrary;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    private void RecreateScene()
    {
        if (ActiveTab?.Stage == null || ActiveTab?.StageRenderer == null) return;

        // Create scene with the stage renderer and all current wall meshes
        var sceneObjects = new List<GameObject> { ActiveTab.StageRenderer };
        sceneObjects.AddRange(ActiveTab.WallMeshes);
        ActiveTab.Scene = new Scene(
            _graphicsDevice,
            sceneObjects,
            activeCamera,
            [] // No shadow cameras for now
        );
    }

    /// <summary>
    /// Rebuilds the ClientStageRenderer from scratch using the current Stage.pieces,
    /// then re-applies the tab's World settings and recreates the Scene + walls.
    /// Call this whenever pieces are added or removed.
    /// </summary>
    private void RebuildClientRenderer()
    {
        if (ActiveTab?.Stage == null) return;

        if (ActiveTab.StageRenderer == null)
            ActiveTab.StageRenderer = new ClientStageRenderer(ActiveTab.Stage);
        else
            ActiveTab.StageRenderer.DetectChanges(true);

        // ClientStageRenderer.ctor calls World.ResetValues(), re-apply our tab settings
        ApplyTabWorldValuesToWorld();
        RecreateEnvironment();
        RecreateScene();

        if (_autoUpdateWalls && HasPlaceableStageGeometry())
            AutoGenerateStageBordersFromGeometry();
        else
            RebuildAllWalls();
    }

    private void ApplyTabWorldValuesToWorld()
    {
        if (ActiveTab == null) return;
        World.SetSky(ActiveTab.SkyColor);
        World.Fog = ActiveTab.FogColor;
        World.GroundColor = ActiveTab.GroundColor;
        // Derives GroundPolysColor, so it has to run before the explicit polys colour below.
        if (ActiveTab.Texture is { Length: 4 } texture)
        {
            World.SetTexture(texture);
            World.HasTexture = true;
        }
        else
        {
            World.HasTexture = false;
        }

        World.FadeFrom = ActiveTab.FadeFrom;
        World.HasPolys = ActiveTab.PolysEnabled;
        World.DrawPolys = ActiveTab.PolysEnabled;
        if (ActiveTab.PolysEnabled)
            World.GroundPolysColor = ActiveTab.PolysColor;
        World.HasClouds = ActiveTab.CloudsEnabled;
        World.DrawClouds = ActiveTab.CloudsEnabled;
        if (ActiveTab.CloudsEnabled)
        {
            World.Clouds =
            [
                ActiveTab.CloudsColor.R, ActiveTab.CloudsColor.G, ActiveTab.CloudsColor.B, ActiveTab.CloudsParam4,
                ActiveTab.CloudsHeight
            ];
            World.CloudCoverage = ActiveTab.CloudCoverage;
        }

        // CloudColor is derived from Sky and Clouds by SetSky; redo it now both are final.
        World.SetSky(World.Sky);

        World.DrawMountains = ActiveTab.MountainsEnabled;
        if (ActiveTab.MountainsEnabled)
            World.MountainSeed = ActiveTab.MountainsSeed;
        World.MountainCoverage = ActiveTab.MountainCoverage ?? 1f;
        // density(n) encodes as 2n+1; with no directive the stage keeps ResetValues' default.
        World.FogDensity = ActiveTab.FogDensity is { } fogDensity ? fogDensity * 2 + 1 : 0.857f;
        World.LightsOn = ActiveTab.LightsOn;
        World.LightDirection = ActiveTab.LightDirection;
        World.Snap = new Color3((short)ActiveTab.SnapA, (short)ActiveTab.SnapB, (short)ActiveTab.SnapC);
    }

    private void RecreateEnvironment()
    {
        if (ActiveTab?.StageRenderer is not { } renderer) return;

        RecreateSky();
        RecreateGround();

        if (ActiveTab.PolysEnabled && ActiveTab.Stage != null)
        {
            if (_autoGeneratePolys)
            {
                renderer.polys?.Dispose();
                renderer.polys = Environment.MakePolys(
                    ActiveTab.Stage,
                    -10000,
                    20000,
                    -10000,
                    20000,
                    ActiveTab.ScenePieces.Count
                );
            }
            // else: preserve existing polys (don't touch) so manually-generated polys from the
            //        Properties dialog survive across piece placements when auto-generate is off.
        }
        else
        {
            renderer.polys?.Dispose();
            renderer.polys = null;
        }

        RecreateScenery();
    }

    /// <summary>The ground quad bakes its colour into the vertex buffer, so it has to be rebuilt.</summary>
    private void RecreateGround()
    {
        if (ActiveTab?.StageRenderer is not { } renderer) return;

        renderer.ground?.Dispose();
        renderer.ground = new Ground(GameSparker.GraphicsDevice);
    }

    /// <summary>The polys mesh bakes the ground/polys colours, so a colour or texture edit rebuilds it.</summary>
    private void RecreatePolys()
    {
        if (ActiveTab?.StageRenderer is not { } renderer || ActiveTab.Stage is null) return;

        renderer.polys?.Dispose();
        renderer.polys = ActiveTab.PolysEnabled
            ? Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count)
            : null;
    }

    /// <summary>The sky gradient bakes the sky and fog colours at construction.</summary>
    private void RecreateSky()
    {
        if (ActiveTab?.StageRenderer is not { } renderer) return;

        renderer.sky?.Dispose();
        renderer.sky = new Sky(GameSparker.GraphicsDevice);
    }

    /// <summary>Clouds and mountains only: their sliders change per tick, unlike polys.</summary>
    private void RecreateScenery()
    {
        if (ActiveTab?.StageRenderer is not { } renderer) return;

        renderer.clouds?.Dispose();
        renderer.clouds = ActiveTab.CloudsEnabled ? Environment.MakeClouds(-10000, 10000, -10000, 10000) : null;

        renderer.mountains?.Dispose();
        renderer.mountains =
            ActiveTab.MountainsEnabled ? Environment.MakeMountains(-10000, 10000, -10000, 10000) : null;
    }

    private void UpdateCameraPosition()
    {
        if (ActiveTab == null) return;

        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
        {
            // First-person flying camera
            float yaw = ActiveTab.CameraYaw * (float)Math.PI / 180f;
            float pitch = ActiveTab.CameraPitch * (float)Math.PI / 180f;

            // Calculate look direction
            var lookDirection = new Vector3(
                (float)(Math.Cos(pitch) * Math.Sin(yaw)),
                (float)Math.Sin(pitch),
                (float)(Math.Cos(pitch) * Math.Cos(yaw))
            );

            activeCamera = perspectiveCamera;
            activeCamera.Near = 50f;
            activeCamera.Far = 1_000_000f;
            if (ActiveTab.Scene != null) ActiveTab.Scene.ActiveCamera = activeCamera;
            activeCamera.PositionWithoutInterpolation = ActiveTab.CameraPosition;
            activeCamera.LookAtWithoutInterpolation = ActiveTab.CameraPosition + lookDirection;
            activeCamera.UpWithoutInterpolation = -Vector3.UnitY;
        }
        else
        {
            // Top down view - look from above at pan position (negative Y is up in this coordinate system)
            activeCamera = ActiveTab.TopDownOrtho ? orthoCamera : perspectiveCamera;
            // Keep top-down rendering visible at very large zoom levels.
            activeCamera.Near = MathF.Max(1f, ActiveTab.TopDownHeight * 0.001f);
            activeCamera.Far = MathF.Max(1_000_000f, ActiveTab.TopDownHeight * 8f + 1_000_000f);
            if (ActiveTab.Scene != null) ActiveTab.Scene.ActiveCamera = activeCamera;
            activeCamera.PositionWithoutInterpolation = new Vector3(
                ActiveTab.TopDownPanPosition.X,
                -ActiveTab.TopDownHeight,
                ActiveTab.TopDownPanPosition.Z
            );
            activeCamera.LookAtWithoutInterpolation = new Vector3(
                ActiveTab.TopDownPanPosition.X,
                0,
                ActiveTab.TopDownPanPosition.Z
            );
            activeCamera.UpWithoutInterpolation = Vector3.UnitZ;
            if (ActiveTab.TopDownOrtho)
            {
                // Match the visible world area that perspective would show at this height.
                // half_height_world = TopDownHeight * tan(Fov/2)
                float halfH = ActiveTab.TopDownHeight * MathF.Tan(perspectiveCamera.Fov * MathF.PI / 180f * 0.5f);
                orthoCamera.OrthoScale = (orthoCamera.Height > 0) ? (2f * halfH / orthoCamera.Height) : 1f;
            }
        }
    }
}
