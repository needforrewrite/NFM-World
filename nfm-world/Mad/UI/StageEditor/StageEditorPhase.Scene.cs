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
        var previousScene = ActiveTab.Scene;
        ActiveTab.Scene = new Scene(
            _graphicsDevice,
            sceneObjects,
            activeCamera,
            [] // No shadow cameras for now
        );
        previousScene?.Dispose();
    }

    /// <summary>
    /// Rebuilds the ClientStageRenderer from scratch using the current Stage.pieces,
    /// then re-applies the tab's World settings and recreates the Scene + walls.
    /// Call this whenever pieces are added or removed.
    /// </summary>
    private void RebuildClientRenderer() => _ = RebuildClientRendererAsync();

    private readonly Dictionary<StageEditorTab, CancellationTokenSource> _rendererRequests = new();

    private async Task RebuildClientRendererAsync()
    {
        if (ActiveTab is not { Stage: not null } tab || _editorDisposed) return;
        if (_rendererRequests.TryGetValue(tab, out var previous)) previous.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_editorLoading.Token);
        _rendererRequests[tab] = cancellation;
        using var loading = LoadingWork.Begin("Preparing editor scene...");
        ClientStageRenderer? next = null;
        try
        {
            next = await ClientStageRenderer.CreateAsync(tab.Stage, cancellation.Token, generateEnvironment: false);
            await LoadingWork.NextFrame;
            while (!_editorDisposed && _tabs.Contains(tab)
                   && (!ReferenceEquals(ActiveTab, tab) || !ReferenceEquals(GameSparker.CurrentPhase, this)))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                await LoadingWork.NextFrame;
            }

            cancellation.Token.ThrowIfCancellationRequested();
            if (_editorDisposed || !_tabs.Contains(tab)) return;
            var old = tab.StageRenderer;
            if (old is not null)
            {
                next.polys = old.polys;
                old.polys = null;
                next.clouds = old.clouds;
                old.clouds = null;
                next.mountains = old.mountains;
                old.mountains = null;
            }

            tab.StageRenderer = next;
            next.Adopt();
            next = null;
            old?.Dispose();
            ApplyTabWorldValuesToWorld();
            RecreateEnvironment();
            RecreateScene();
            if (_autoUpdateWalls && HasPlaceableStageGeometry()) AutoGenerateStageBordersFromGeometry();
            else RebuildAllWalls();
            Touch();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not prepare editor scene: {ex.Message}");
        }
        finally
        {
            await LoadingWork.NextFrame;
            next?.Dispose();
            if (_rendererRequests.GetValueOrDefault(tab) == cancellation) _rendererRequests.Remove(tab);
        }
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
            if (_autoGeneratePolys) RecreatePolys();
            // else: preserve existing polys (don't touch) so manually-generated polys from the
            //        Properties dialog survive across piece placements when auto-generate is off.
        }
        else
        {
            RecreatePolys();
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

        var tab = ActiveTab;
        var settings = Environment.GenerationSettings.Capture();
        var enabled = tab.PolysEnabled;
        var count = tab.ScenePieces.Count;
        _ = RecreateEnvironmentPartAsync(
            tab,
            renderer,
            0,
            () => enabled ? Environment.BuildPolys(tab.Stage, -10000, 20000, -10000, 20000, count, settings) : null
        );
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

        var tab = ActiveTab;
        var settings = Environment.GenerationSettings.Capture();
        var clouds = tab.CloudsEnabled;
        var mountains = tab.MountainsEnabled;
        _ = RecreateEnvironmentPartAsync(
            tab,
            renderer,
            1,
            () => clouds ? Environment.BuildClouds(-10000, 10000, -10000, 10000, settings) : null
        );
        _ = RecreateEnvironmentPartAsync(
            tab,
            renderer,
            2,
            () => mountains ? Environment.BuildMountains(-10000, 10000, -10000, 10000, settings) : null
        );
    }

    private readonly Dictionary<(StageEditorTab Tab, int Part), CancellationTokenSource> _environmentRequests = new();
    private readonly SemaphoreSlim _environmentGate = new(1, 1);

    private async Task RecreateEnvironmentPartAsync(
        StageEditorTab tab,
        ClientStageRenderer renderer,
        int part,
        Func<NFMWorldLibrary.Rad.Rad3dPoly[]?> build
    )
    {
        var key = (tab, part);
        if (_environmentRequests.TryGetValue(key, out var previous)) previous.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_editorLoading.Token);
        _environmentRequests[key] = cancellation;
        using var loading = LoadingWork.Begin("Preparing scenery...");
        try
        {
            await _environmentGate.WaitAsync(cancellation.Token);
            EnvironmentMeshData? polygons;
            try
            {
                polygons = await Task.Run(
                    () => build() is { } geometry ? EnvironmentMeshData.Prepare(geometry) : null,
                    cancellation.Token
                );
            }
            finally
            {
                _environmentGate.Release();
            }

            await LoadingWork.NextFrame;
            cancellation.Token.ThrowIfCancellationRequested();
            if (_editorDisposed || !ReferenceEquals(tab.StageRenderer, renderer)) return;
            switch (part)
            {
                case 0:
                    var polys = polygons is null ? null : new GroundPolys(_graphicsDevice, polygons);
                    renderer.polys?.Dispose();
                    renderer.polys = polys;
                    break;
                case 1:
                    var clouds = polygons is null ? null : new GroundPolys(_graphicsDevice, polygons);
                    renderer.clouds?.Dispose();
                    renderer.clouds = clouds;
                    break;
                case 2:
                    var mountains = polygons is null ? null : new Mountains(_graphicsDevice, polygons);
                    renderer.mountains?.Dispose();
                    renderer.mountains = mountains;
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not prepare scenery: {ex.Message}");
        }
        finally
        {
            await LoadingWork.NextFrame;
            if (_environmentRequests.GetValueOrDefault(key) == cancellation) _environmentRequests.Remove(key);
        }
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
