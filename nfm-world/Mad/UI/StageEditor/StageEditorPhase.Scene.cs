using System.Collections.ObjectModel;
using Hexa.NET.ImGui;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    private void RecreateScene()
    {
        if (ActiveTab?.Stage == null || ActiveTab?.StageRenderer == null) return;
        
        // TODO(Milestone 5 Stage B follow-up): Scene now takes NFMWorld.Graphics.IGraphicsDevice,
        // not FNA's GraphicsDevice - StageEditorPhase's own _graphicsDevice field (and its wider
        // direct-GraphicsDevice usage) isn't converted yet, so this leaves ActiveTab.Scene null
        // (its declared type is already nullable) rather than constructing a real Scene. Nothing
        // exercises the stage editor today (GameSparker.Load isn't called - see WorldGame.cs's
        // Stage A TODOs).
        ActiveTab.Scene = null;
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
            ActiveTab.StageRenderer = new ClientStageRenderer(_graphicsDevice, ActiveTab.Stage);
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
        World.Sky = ActiveTab.SkyColor;
        World.Fog = ActiveTab.FogColor;
        World.GroundColor = ActiveTab.GroundColor;
        World.FadeFrom = ActiveTab.FadeFrom;
        World.HasPolys = ActiveTab.PolysEnabled;
        World.DrawPolys = ActiveTab.PolysEnabled;
        if (ActiveTab.PolysEnabled)
            World.GroundPolysColor = ActiveTab.PolysColor;
        World.HasClouds = ActiveTab.CloudsEnabled;
        World.DrawClouds = ActiveTab.CloudsEnabled;
        if (ActiveTab.CloudsEnabled)
        {
            World.Clouds = [ActiveTab.CloudsColor.R, ActiveTab.CloudsColor.G, ActiveTab.CloudsColor.B, ActiveTab.CloudsParam4, ActiveTab.CloudsHeight];
            World.CloudCoverage = ActiveTab.CloudCoverage;
        }
        World.DrawMountains = ActiveTab.MountainsEnabled;
        if (ActiveTab.MountainsEnabled)
            World.MountainSeed = ActiveTab.MountainsSeed;
        World.Snap = new Color3((short)ActiveTab.SnapA, (short)ActiveTab.SnapB, (short)ActiveTab.SnapC);
    }
    
    private void RecreateEnvironment()
    {
        if (ActiveTab?.StageRenderer == null) return;
        // TODO(Milestone 5 Stage B follow-up): _graphicsDevice here is still FNA's XNA-typed
        // GraphicsDevice (StageEditorPhase is out of scope for this pass); Sky/Ground now take
        // IGraphicsDevice. Not reached today since GameSparker.Load never runs.
        ActiveTab.StageRenderer.sky = new Sky(GameSparker.NewGraphicsDevice);
        ActiveTab.StageRenderer.ground = new Ground(GameSparker.NewGraphicsDevice);
        if (ActiveTab.PolysEnabled && ActiveTab.Stage != null)
        {
            if (_autoGeneratePolys)
                ActiveTab.StageRenderer.polys = Environment.MakePolys(ActiveTab.Stage, -10000, 20000, -10000, 20000, ActiveTab.ScenePieces.Count, _graphicsDevice);
            // else: preserve existing polys (don't touch) so manually-generated polys from the
            //        Properties dialog survive across piece placements when auto-generate is off.
        }
        else
            ActiveTab.StageRenderer.polys = null;
        if (ActiveTab.CloudsEnabled)
            ActiveTab.StageRenderer.clouds = Environment.MakeClouds(-10000, 10000, -10000, 10000, _graphicsDevice);
        else
            ActiveTab.StageRenderer.clouds = null;
        if (ActiveTab.MountainsEnabled)
            ActiveTab.StageRenderer.mountains = Environment.MakeMountains(-10000, 10000, -10000, 10000, _graphicsDevice);
        else
            ActiveTab.StageRenderer.mountains = null;
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
            activeCamera.PositionWithoutInterpolation = new Vector3(ActiveTab.TopDownPanPosition.X, -ActiveTab.TopDownHeight, ActiveTab.TopDownPanPosition.Z);
            activeCamera.LookAtWithoutInterpolation = new Vector3(ActiveTab.TopDownPanPosition.X, 0, ActiveTab.TopDownPanPosition.Z);
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
