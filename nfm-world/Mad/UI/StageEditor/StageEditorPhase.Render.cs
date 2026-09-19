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
        
        // Clear with appropriate background color based on view mode
        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
        {
            // Gray background for top-down view
            _graphicsDevice.Clear(new Color(128, 128, 128).ToXna());
        }
        else
        {
            // Sky blue background for 3D scene view
            _graphicsDevice.Clear(new Color(135, 206, 235).ToXna());
        }
        
        // Set up scissor rectangle to only render within the viewport area
        var oldScissorRect = _graphicsDevice.ScissorRectangle;
        var oldRasterizerState = _graphicsDevice.RasterizerState;
        
        // Only set scissor if we have valid viewport bounds
        if (_viewportMax.X > _viewportMin.X && _viewportMax.Y > _viewportMin.Y)
        {
            var scissorRect = new Rectangle(
                (int)_viewportMin.X,
                (int)_viewportMin.Y,
                (int)(_viewportMax.X - _viewportMin.X),
                (int)(_viewportMax.Y - _viewportMin.Y)
            );
            
            var rasterizerState = new RasterizerState
            {
                CullMode = CullMode.CullCounterClockwiseFace,
                ScissorTestEnable = true
            };
            
            _graphicsDevice.ScissorRectangle = scissorRect;
            _graphicsDevice.RasterizerState = rasterizerState;
        }
        
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
                ActiveTab?.Scene.Render(null!, alpha, false); // TODO(Milestone 5 Stage B follow-up): ActiveTab.Scene is currently always null; never reached today.
                
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
                ActiveTab?.Scene.Render(null!, alpha, false); // TODO(Milestone 5 Stage B follow-up): ActiveTab.Scene is currently always null; never reached today.
            }
        }
        
        // Render wall meshes separately (editor-only visualization) - BEFORE restoring scissor state
        if (ActiveTab != null)
        // Wall meshes are now part of the Scene (added in RecreateScene), no separate render needed
        
        // Restore old state
        _graphicsDevice.ScissorRectangle = oldScissorRect;
        _graphicsDevice.RasterizerState = oldRasterizerState;
        
        // Render selection highlight for all selected pieces, gizmo on primary
        RenderSelectionHighlights(ActiveTab);
        RenderSelectedWallHighlight(ActiveTab);
        if (ActiveTab.ActivePieceId >= 0)
        {
            var selectedPiece = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
            if (selectedPiece?.Obj != null)
                Debug.RenderGizmo(ComputeSelectionCentroid(), activeCamera, ref _gizmoHovered, ref _gizmoDragging, new Vector2(_mouseX, _mouseY));
        }
        
        // Process pending preview thumbnails
        while (_previewQueue.Count > 0) 
            ProcessOnePreviewThumbnail();
        
        // Render placement ghost if in placement mode and mouse is over viewport
        if (_pendingPlacementPartIndex >= 0 && _hasValidPlacementPos)
            RenderPlacementPreview();
        
        // Clear the depth buffer so ImGui always renders on top of the 3D scene.
        // Without this, geometry close to the camera writes near-zero depth values and
        // ImGui pixels (rendered later with DepthRead) fail the depth test at those positions.
        _graphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black.ToXna(), 1.0f, 0);
    }
    
}
