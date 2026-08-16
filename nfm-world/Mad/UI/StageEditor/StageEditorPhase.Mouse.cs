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
    public override void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        if (!GameSparker.Game.IsActive) return;
        if (!_isOpen) return;
        
        // Handle gizmo dragging before anything else
        if (_gizmoDragging != GizmoAxis.None && ActiveTab != null)
        {
            var selectedPiece = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
            if (selectedPiece != null)
            {
                int dx = x - _gizmoDragStartX;
                int dy = y - _gizmoDragStartY;
                
                if (_gizmoDragging == GizmoAxis.X)
                {
                    // Project the gizmo arrow from the centroid to get pixels-per-world-unit ratio
                    var piecePos = new Vector3(_gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ);
                    var gizmoMetrics = Debug.ComputeGizmoMetrics(piecePos, activeCamera);
                    if (Debug.WorldToScreen(piecePos, out var ss0, activeCamera) && Debug.WorldToScreen(piecePos + new Vector3(gizmoMetrics.ArrowLength, 0, 0), out var ss1, activeCamera))
                    {
                        var screenArrow = ss1 - ss0;
                        float screenLen = screenArrow.Length();
                        if (screenLen > 1f)
                        {
                            var axisDir = screenArrow / screenLen;
                            float pixelDelta = Vector2.Dot(new Vector2(dx, dy), axisDir);
                            float worldDelta = pixelDelta * (gizmoMetrics.ArrowLength / screenLen);
                            // Apply delta to every selected piece using its own start position
                            foreach (var (sid, spos) in _gizmoDragStartPositions)
                            {
                                var sp = ActiveTab.ScenePieces.GetValueOrDefault(sid);
                                if (sp == null) continue;
                                float newX = (float)spos.X + worldDelta;
                                if (_isShiftPressed != _gridSnapEnabled && _gridSnapSize > 0f)
                                    newX = MathF.Round(newX / _gridSnapSize) * _gridSnapSize;
                                sp.Position = new f64Vector3((fix64)newX, spos.Y, spos.Z);
                            }
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                }
                else if (_gizmoDragging == GizmoAxis.Y)
                {
                    // Y axis: project the upward arrow (world -Y direction) to screen
                    var piecePos = new Vector3(_gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ);
                    var gizmoMetrics = Debug.ComputeGizmoMetrics(piecePos, activeCamera);
                    if (Debug.WorldToScreen(piecePos, out var ss0, activeCamera) && Debug.WorldToScreen(piecePos + new Vector3(0, -gizmoMetrics.ArrowLength, 0), out var ss1, activeCamera))
                    {
                        var screenArrow = ss1 - ss0;
                        float screenLen = screenArrow.Length();
                        if (screenLen > 1f)
                        {
                            var axisDir = screenArrow / screenLen;
                            float pixelDelta = Vector2.Dot(new Vector2(dx, dy), axisDir);
                            // Moving up on screen decreases world Y (camera is flipped), so negate
                            float worldDelta = -pixelDelta * (gizmoMetrics.ArrowLength / screenLen);
                            foreach (var (sid, spos) in _gizmoDragStartPositions)
                            {
                                var sp = ActiveTab.ScenePieces.GetValueOrDefault(sid);
                                if (sp == null) continue;
                                float newY = (float)spos.Y + worldDelta;
                                if (_isShiftPressed != _gridSnapEnabled && _gridSnapSize > 0f)
                                    newY = MathF.Round(newY / _gridSnapSize) * _gridSnapSize;
                                sp.Position = new f64Vector3(spos.X, (fix64)newY, spos.Z);
                            }
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                }
                else if (_gizmoDragging == GizmoAxis.Z)
                {
                    var piecePos = new Vector3(_gizmoCentroidX, _gizmoCentroidY, _gizmoCentroidZ);
                    var gizmoMetrics = Debug.ComputeGizmoMetrics(piecePos, activeCamera);
                    if (Debug.WorldToScreen(piecePos, out var ss0, activeCamera) && Debug.WorldToScreen(piecePos + new Vector3(0, 0, gizmoMetrics.ArrowLength), out var ss1, activeCamera))
                    {
                        var screenArrow = ss1 - ss0;
                        float screenLen = screenArrow.Length();
                        if (screenLen > 1f)
                        {
                            var axisDir = screenArrow / screenLen;
                            float pixelDelta = Vector2.Dot(new Vector2(dx, dy), axisDir);
                            float worldDelta = pixelDelta * (gizmoMetrics.ArrowLength / screenLen);
                            foreach (var (sid, spos) in _gizmoDragStartPositions)
                            {
                                var sp = ActiveTab.ScenePieces.GetValueOrDefault(sid);
                                if (sp == null) continue;
                                float newZ = (float)spos.Z + worldDelta;
                                if (_isShiftPressed != _gridSnapEnabled && _gridSnapSize > 0f)
                                    newZ = MathF.Round(newZ / _gridSnapSize) * _gridSnapSize;
                                sp.Position = new f64Vector3(spos.X, spos.Y, (fix64)newZ);
                            }
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                }
                else if (_gizmoDragging == GizmoAxis.RotY)
                {
                    // Angle delta based on horizontal drag
                    float angleDelta = dx * 0.5f; // degrees per pixel
                    float radians = angleDelta * MathF.PI / 180f;
                    float cosA = MathF.Cos(radians);
                    float sinA = MathF.Sin(radians);
                    // Rotate every selected piece's position around the centroid and its own yaw
                    foreach (var (sid, startPos) in _gizmoDragStartPositions)
                    {
                        var sp = ActiveTab.ScenePieces.GetValueOrDefault(sid);
                        if (sp == null) continue;
                        float relX = (float)startPos.X - _gizmoCentroidX;
                        float relZ = (float)startPos.Z - _gizmoCentroidZ;
                        float newRelX = relX * cosA - relZ * sinA;
                        float newRelZ = relX * sinA + relZ * cosA;
                        sp.Position = new f64Vector3(
                            (fix64)(_gizmoCentroidX + newRelX),
                            startPos.Y,
                            (fix64)(_gizmoCentroidZ + newRelZ));
                        float startRot = _gizmoDragStartRotations.TryGetValue(sid, out var r) ? r : 0f;

                        fix64 rot = (fix64)((startRot + angleDelta) % 360f);
                        if (_isShiftPressed)
                        {
                            // snap to 15deg
                            rot = fix64.Round(rot / 15) * 15;
                        }
                        
                        sp.Rotation = new f64Euler(f64AngleSingle.FromDegrees(rot), sp.Rotation.Pitch, sp.Rotation.Roll);
                    }
                    ActiveTab.HasUnsavedChanges = true;
                }
            }
            _mouseX = x; _mouseY = y;
            return;
        }
        
        _mouseX = x;
        _mouseY = y;
        
        // Update rect selection end while LMB is held
        if (_isRectSelecting)
        {
            _rectSelectEndX = x;
            _rectSelectEndY = y;
        }
        
        // Update placement preview position while hovering over the viewport
        if (_pendingPlacementPartIndex >= 0)
        {
            if (IsMouseInViewport(x, y))
            {
                _hasValidPlacementPos = TryGetGroundPositionAtMouse(x, y, out var groundPos);
                if (_hasValidPlacementPos)
                {
                    float gx = groundPos.X;
                    float gz = groundPos.Z;
                    if (_gridSnapEnabled && _gridSnapSize > 0f)
                    {
                        gx = MathF.Round(gx / _gridSnapSize) * _gridSnapSize;
                        gz = MathF.Round(gz / _gridSnapSize) * _gridSnapSize;
                    }

                    if (_snapEnabled && ActiveTab != null)
                    {
                        foreach (var piece in ActiveTab.ScenePieces)
                        {
                            if (piece.Rad.AtLines is { } atLines)
                            {
                                foreach (var (direction, offset) in atLines)
                                {
                                    // Check if the line is close enough to snap to
                                    var lineDir = direction == AttachmentLineDirection.X
                                        ? new Vector2(1, 0).RotateXz(piece.Rotation.Xz.Degrees)
                                        : new Vector2(0, 1).RotateXz(piece.Rotation.Xz.Degrees);
                                    var linePoint = new Vector2(
                                        (float)piece.Position.X + (lineDir.X == 0 ? 0 : (float)offset),
                                        (float)piece.Position.Z + (lineDir.Y == 0 ? 0 : (float)offset));
                                    var toPoint = new Vector2(gx, gz) - linePoint;
                                    float projLen = Vector2.Dot(toPoint, lineDir);
                                    var closestPoint = linePoint + lineDir * projLen;
                                    float dist = Vector2.Distance(new Vector2(gx, gz), closestPoint);
                                    if (dist < 500) // snap threshold in world units
                                    {
                                        gx = closestPoint.X;
                                        gz = closestPoint.Y;
                                        
                                        if (_gridSnapEnabled && _gridSnapSize > 0f)
                                        {
                                            gx = MathF.Round(gx / _gridSnapSize) * _gridSnapSize;
                                            gz = MathF.Round(gz / _gridSnapSize) * _gridSnapSize;
                                        }
                                    }
                                }
                            }
                            
                            foreach (var atp in piece.Rad.Atp)
                            {
                                // Check if the hitbox is close enough to snap to
                                var hbCenter = new Vector2((float)piece.Position.X, (float)piece.Position.Z)
                                               + new Vector2((float)atp.X, (float)atp.Y).RotateXz(piece.Rotation.Xz.Degrees);
                                float dist = Vector2.Distance(new Vector2(gx, gz), hbCenter);
                                if (dist < 500) // snap threshold in world units
                                {
                                    gx = hbCenter.X;
                                    gz = hbCenter.Y;

                                    goto snapped;
                                }

                                foreach (var ownAtp in _availableParts[_pendingPlacementPartIndex].Atp)
                                {
                                    // try to attach the two points together

                                    var ownHbCenter = new Vector2(gx, gz) + new Vector2((float)ownAtp.X, (float)ownAtp.Y).RotateXz((fix64)_pendingPlacementYaw);
                                    
                                    dist = Vector2.Distance(ownHbCenter, hbCenter);
                                    if (dist < 500) // snap threshold in world units
                                    {
                                        gx += hbCenter.X - ownHbCenter.X;
                                        gz += hbCenter.Y - ownHbCenter.Y;
                                        goto snapped;
                                    }
                                }
                            }
                            
                            snapped: ;
                        }
                    }
                    
                    _pendingPlacementPos = new f64Vector3((fix64)gx, (fix64)groundPos.Y + _pendingPlacementYOff, (fix64)gz);
                }
            }
            else
            {
                _hasValidPlacementPos = false;
            }
        }
        
        // Check if right mouse button is currently held down
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        bool isRightButtonHeld = mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        
        // Start dragging if right button is held, we're in viewport, in Scene view, and not already dragging
        if (isRightButtonHeld && IsMouseInViewport(x, y) && ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene && !_isRightDragging)
        {
            _isRightDragging = true;
            _rightDragStartX = x;
            _rightDragStartY = y;
            _rightDragStartYaw = ActiveTab.CameraYaw;
            _rightDragStartPitch = ActiveTab.CameraPitch;
        }
        
        // Handle right-click drag for camera rotation (only in Scene view)
        if (_isRightDragging && isRightButtonHeld && !imguiWantsMouse && ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
        {
            int deltaX = x - _rightDragStartX;
            int deltaY = y - _rightDragStartY;
            
            ActiveTab.CameraYaw = _rightDragStartYaw + deltaX * 0.5f;
            ActiveTab.CameraPitch = Math.Clamp(_rightDragStartPitch + deltaY * 0.5f, -89f, 89f); // Inverted pitch
            
            UpdateCameraPosition();
        }
        
        // Stop dragging if right button is released
        if (!isRightButtonHeld && _isRightDragging)
        {
            _isRightDragging = false;
        }
    }
    
    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        
        if (imguiWantsMouse) return;
        if (!GameSparker.Game.IsActive) return;
        if (!_isOpen) return;
        
        _mouseX = x;
        _mouseY = y;
        
        // Check if it's right mouse button via Microsoft.Xna.Framework.Input.Mouse
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        
        if (mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            // Right-click for camera rotation (only in Scene view)
            _isRightButtonDown = true;
            
            if (IsMouseInViewport(x, y) && !_isRightDragging && ActiveTab?.ViewMode == StageEditorTab.ViewModeEnum.Scene)
            {
                _isRightDragging = true;
                _rightDragStartX = x;
                _rightDragStartY = y;
                _rightDragStartYaw = ActiveTab.CameraYaw;
                _rightDragStartPitch = ActiveTab.CameraPitch;
            }
        }
        
        if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            // Left-click
            _isLeftButtonDown = true;
            
            // Check if clicking a gizmo handle first
            if (_gizmoHovered != GizmoAxis.None && ActiveTab != null && ActiveTab.ActivePieceId >= 0)
            {
                var piece = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
                if (piece != null)
                {
                    PushUndoSnapshot();
                    _gizmoDragging = _gizmoHovered;
                    _gizmoDragStartX = x;
                    _gizmoDragStartY = y;
                    _gizmoDragStartRotY = (float)piece.Rotation.Yaw.Degrees;
                    // Compute centroid of the whole selection as pivot
                    var centroid = ComputeSelectionCentroid();
                    _gizmoCentroidX = centroid.X;
                    _gizmoCentroidY = centroid.Y;
                    _gizmoCentroidZ = centroid.Z;
                    _gizmoDragStartPosX = centroid.X;
                    _gizmoDragStartPosY = centroid.Y;
                    _gizmoDragStartPosZ = centroid.Z;
                    // Capture start positions of all selected pieces for group drag
                    _gizmoDragStartPositions.Clear();
                    _gizmoDragStartRotations.Clear();
                    foreach (var selId in ActiveTab.SelectedPieceIds)
                    {
                        var selP = ActiveTab.ScenePieces.GetValueOrDefault(selId);
                        if (selP != null)
                        {
                            _gizmoDragStartPositions[selId] = selP.Position;
                            _gizmoDragStartRotations[selId] = (float)selP.Rotation.Yaw.Degrees;
                        }
                    }
                }
            }
            else if (IsMouseInViewport(x, y) && _pendingPlacementPartIndex < 0 && !_isSwapMode)
            {
                // Begin potential rect selection
                _isRectSelecting = true;
                _rectSelectStartX = x;
                _rectSelectStartY = y;
                _rectSelectEndX = x;
                _rectSelectEndY = y;
            }
        }
        
        if (mouseState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            // In placement mode
            if (_pendingPlacementPartIndex >= 0)
            {
                _pendingPlacementYaw = (_pendingPlacementYaw + 90) % 360f;
            }
        }
    }
    
    public override void MouseScrolled(int x, int y, int delta, bool imguiWantsMouse, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseScrolled(x, y, delta, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);

        if (imguiWantsMouse) return;
        if (!GameSparker.Game.IsActive) return;
        if (!_isOpen) return;
        
        // In placement mode
        if (_pendingPlacementPartIndex >= 0)
        {
            if (_isShiftPressed)
            {
                _pendingPlacementYOff = (int)(_pendingPlacementYOff + (delta / 120f) * 50f);
                return;
            }
            else if (_isCtrlPressed)
            {
                _pendingPlacementYaw = (_pendingPlacementYaw + (delta / 120f) * 15f) % 360f;
                return;
            }
        }

        // Only act if mouse is in viewport
        if (IsMouseInViewport(_mouseX, _mouseY))
        {
            if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
            {
                // Exponential zoom gives a consistent feel across near/far ranges.
                float wheelSteps = delta / 120f;
                float zoomFactor = MathF.Pow(1.15f, -wheelSteps);
                ActiveTab.TopDownHeight = MathF.Max(500f, ActiveTab.TopDownHeight * zoomFactor);
                UpdateCameraPosition();
            }
            else
            {
                // Keep old distance tracking for compatibility
                ActiveTab.CameraDistance = Math.Clamp(ActiveTab.CameraDistance - delta * 50f, 100f, 10000f);
            }
        }
    }
    
    public override void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        
        // Check if it's right mouse button
        var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
        
        if (mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _isRightButtonDown)
        {
            _isRightButtonDown = false;
            
            if (_isRightDragging)
            {
                _isRightDragging = false;
            }
            else if (_pendingPlacementPartIndex >= 0)
            {
                // Right-click cancels placement mode
                _pendingPlacementPartIndex = -1;
                _hasValidPlacementPos = false;
            }
        }
        else if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _isLeftButtonDown)
        {
            _isLeftButtonDown = false;
            
            // If we were dragging gizmo, stop and skip ray picking
            if (_gizmoDragging != GizmoAxis.None)
            {
                _gizmoDragging = GizmoAxis.None;
                return;
            }
        
            // Finalise rect selection if active
            if (_isRectSelecting)
            {
                _isRectSelecting = false;
                int rw = Math.Abs(_rectSelectEndX - _rectSelectStartX);
                int rh = Math.Abs(_rectSelectEndY - _rectSelectStartY);
                bool isRectDrag = rw > 5 || rh > 5;
                
                if (isRectDrag && ActiveTab != null && !imguiWantsMouse)
                {
                    int minX = Math.Min(_rectSelectStartX, _rectSelectEndX);
                    int maxX = Math.Max(_rectSelectStartX, _rectSelectEndX);
                    int minY = Math.Min(_rectSelectStartY, _rectSelectEndY);
                    int maxY = Math.Max(_rectSelectStartY, _rectSelectEndY);
                    
                    if (!_isShiftPressed) ActiveTab.SelectedPieceIds.Clear();
                    int lastId = -1;
                    foreach (var piece in ActiveTab.ScenePieces)
                    {
                        if (piece.Obj == null) continue;
                        var wp = new Vector3((float)piece.Position.X, (float)piece.Position.Y, (float)piece.Position.Z);
                        if (Debug.WorldToScreen(wp, out var sp, activeCamera))
                        {
                            if (sp.X >= minX && sp.X <= maxX && sp.Y >= minY && sp.Y <= maxY)
                            {
                                ActiveTab.SelectedPieceIds.Add(piece.Id);
                                lastId = piece.Id;
                            }
                        }
                    }
                    if (lastId >= 0)
                    {
                        ActiveTab.ActivePieceId = lastId;
                        ActiveTab.SelectedWallId = -1;
                    }
                    return;
                }
                // else fall through to normal single click logic
            }
        
            // Handle piece selection on left click
            if (!imguiWantsMouse && IsMouseInViewport(x, y))
            {
                // Placement mode: spawn the part at the hovered ground position
                if (_pendingPlacementPartIndex >= 0)
                {
                    if (_hasValidPlacementPos && ActiveTab?.Stage != null)
                    {
                        var pendingPart = _availableParts[_pendingPlacementPartIndex];
                        var placementRot = new f64Euler(
                            f64AngleSingle.FromDegrees((fix64)_pendingPlacementYaw),
                            f64AngleSingle.ZeroAngle,
                            f64AngleSingle.ZeroAngle);
                        var newMesh = StageObject.CreateDefaultObject(pendingPart, _pendingPlacementPos + new f64Vector3(0, _pendingPlacementYOff, 0), placementRot);
                        var instance = new StagePieceInstance(newMesh, ActiveTab.GetNextPieceId());
                        PushUndoSnapshot();
                        ActiveTab.ScenePieces.Add(instance);
                        ActiveTab.Stage.Pieces[ActiveTab.Stage.StagePartCount] = newMesh;
                        ActiveTab.ActivePieceId = instance.Id;
                        ActiveTab.SelectedPieceIds.Clear();
                        ActiveTab.SelectedPieceIds.Add(instance.Id);
                        ActiveTab.HasUnsavedChanges = true;
                        RebuildClientRenderer();
                        // Stay in placement mode so the user can keep placing the same part
                    }
                    return; // Don't do ray picking while in placement mode
                }

                if (ActiveTab?.Stage != null)
                {
                    var pickedPieceId = PerformRayPicking(x, y);
                    if (pickedPieceId >= 0)
                    {
                        if (_isCtrlPressed)
                        {
                            // Toggle in multi-selection
                            if (!ActiveTab.SelectedPieceIds.Remove(pickedPieceId))
                                ActiveTab.SelectedPieceIds.Add(pickedPieceId);
                        }
                        else
                        {
                            ActiveTab.SelectedPieceIds.Clear();
                            ActiveTab.SelectedPieceIds.Add(pickedPieceId);
                        }

                        ActiveTab.ActivePieceId = pickedPieceId;
                        ActiveTab.SelectedWallId = -1;
                    }
                    else
                    {
                        var pickedWallId = PerformWallRayPicking(x, y);
                        if (pickedWallId >= 0)
                        {
                            ActiveTab.SelectedWallId = pickedWallId;
                            ActiveTab.ActivePieceId = -1;
                            ActiveTab.SelectedPieceIds.Clear();
                        }
                        else if (!_isCtrlPressed)
                        {
                            ActiveTab.SelectedPieceIds.Clear();
                            ActiveTab.ActivePieceId = -1;
                        }
                    }
                }
            }
        }
    }
    
}
