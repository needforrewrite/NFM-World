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
    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        if (!_isOpen) return;
        if (ActiveTab == null) return;

        // Pending top-down export (requested from the export dialog, see _exportRequested).
        if (_exportRequested)
        {
            _exportRequested = false;
            ExportTopDownImage(cb);
        }
        
        // Clear with appropriate background color based on view mode
        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
        {
            // Gray background for top-down view
            cb.Clear(NFMWorld.Graphics.ClearOptions.Color, new NFMWorld.Graphics.ColorRgba(128 / 255f, 128 / 255f, 128 / 255f));
        }
        else
        {
            // Sky blue background for 3D scene view
            cb.Clear(NFMWorld.Graphics.ClearOptions.Color, new NFMWorld.Graphics.ColorRgba(135 / 255f, 206 / 255f, 235 / 255f));
        }
        
        // TODO(clip): the 3D view is not yet clipped to the ImGui viewport rect. It used to be a
        // hardware scissor (GraphicsDevice.ScissorRectangle + a ScissorTestEnable rasterizer state);
        // in the new model the scissor test is baked per-pipeline, so this needs the pipeline set to
        // opt in first - see the milestone plan's scissor stage. _viewportMin/_viewportMax are kept
        // for that step and are still maintained by the panels that compute them.
        
        // Render the 3D scene
        if (ActiveTab?.Scene != null && ActiveTab?.Stage != null && ActiveTab?.StageRenderer != null)
        {
            if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
            {
                // Top-down view: with lighting, no sky/ground/polys/clouds/mountains
                var oldGround = ActiveTab?.StageRenderer.ground;
                var oldSky = ActiveTab?.StageRenderer.sky;
                var oldPolys = ActiveTab?.StageRenderer.polys;
                var oldClouds = ActiveTab?.StageRenderer.clouds;
                var oldMountains = ActiveTab?.StageRenderer.mountains;
                var oldFadeFrom = World.FadeFrom;
                float requestedFade = ActiveTab.TopDownHeight * 24f;
                int topDownFadeFrom = requestedFade >= int.MaxValue
                    ? int.MaxValue
                    : Math.Max(10_000, (int)MathF.Ceiling(requestedFade));
                
                // Temporarily remove environment elements and suppress fog
                ActiveTab?.StageRenderer.ground = null!;
                ActiveTab?.StageRenderer.sky = null!;
                ActiveTab?.StageRenderer.polys = null;
                ActiveTab?.StageRenderer.clouds = null;
                ActiveTab?.StageRenderer.mountains = null;
                World.FadeFrom = Math.Max(oldFadeFrom, topDownFadeFrom);
                
                // Render with lighting preserved
                ActiveTab?.Scene.Render(cb, alpha, false); // TODO(Milestone 5 Stage B follow-up): ActiveTab.Scene is currently always null; never reached today.
                
                // Restore environment elements
                ActiveTab?.StageRenderer.ground = oldGround;
                ActiveTab?.StageRenderer.sky = oldSky;
                ActiveTab?.StageRenderer.polys = oldPolys;
                ActiveTab?.StageRenderer.clouds = oldClouds;
                ActiveTab?.StageRenderer.mountains = oldMountains;
                World.FadeFrom = oldFadeFrom;
            }
            else
            {
                // Normal 3D view with lighting and ground
                ActiveTab?.Scene.Render(cb, alpha, false); // TODO(Milestone 5 Stage B follow-up): ActiveTab.Scene is currently always null; never reached today.
            }
        }
        
        // Render wall meshes separately (editor-only visualization) - BEFORE restoring scissor state
        if (ActiveTab != null)
        // Wall meshes are now part of the Scene (added in RecreateScene), no separate render needed
        
        // Render selection highlight for all selected pieces, gizmo on primary
        RenderSelectionHighlights(cb, ActiveTab);
        RenderSelectedWallHighlight(cb, ActiveTab);
        if (ActiveTab.ActivePieceId >= 0)
        {
            var selectedPiece = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
            if (selectedPiece?.Obj != null)
                Debug.RenderGizmo(cb, GameSparker.NewGraphicsDevice, ComputeSelectionCentroid(), activeCamera, ref _gizmoHovered, ref _gizmoDragging, new Vector2(_mouseX, _mouseY));
        }
        
        // Process pending preview thumbnails
        while (_previewQueue.Count > 0) 
            ProcessOnePreviewThumbnail(cb);
        
        // Render placement ghost if in placement mode and mouse is over viewport
        if (_pendingPlacementPartIndex >= 0 && _hasValidPlacementPos)
            RenderPlacementPreview(cb);
        
        // Clear the depth buffer so ImGui always renders on top of the 3D scene.
        // Without this, geometry close to the camera writes near-zero depth values and
        // ImGui pixels (rendered later with DepthRead) fail the depth test at those positions.
        cb.Clear(NFMWorld.Graphics.ClearOptions.Depth, default, 1f, 0);
    }
    
}
