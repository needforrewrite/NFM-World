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
    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        if (imguiWantsKeyboard) return;
        if (!_isOpen) return;
        
        // In placement mode
        if (_pendingPlacementPartIndex >= 0)
        {
            // Q/E rotate the pending piece by 45°. Q is also the camera-down key so we handle rotation first and skip the camera binding.
            if (key == Key.E)
            {
                _pendingPlacementYaw = (_pendingPlacementYaw + 45f) % 360f;
                return;
            }
            if (key == Key.Q)
            {
                _pendingPlacementYaw = ((_pendingPlacementYaw - 45f) % 360f + 360f) % 360f;
                return;
            }
            if (key == Key.R)
            {
                // Reset rotation
                _pendingPlacementYaw = 0f;
                return;
            }

            if (key == Key.G)
            {
                _gridSnapEnabled = !_gridSnapEnabled;
                return;
            }
        }
        
        // Camera movement
        switch (key)
        {
            case Key.W:
                _moveForward = true;
                break;
            case Key.S:
                _moveBackward = true;
                break;
            case Key.A:
                _moveLeft = true;
                break;
            case Key.D:
                _moveRight = true;
                break;
            case Key.Space:
                _moveUp = true;
                break;
            case Key.Q:
                _moveDown = true;
                break;
            case Key.LShiftKey:
            case Key.RShiftKey:
                _isShiftPressed = true;
                break;
            case Key.LControlKey:
            case Key.RControlKey:
                _isCtrlPressed = true;
                break;
        }
        
        // Handle keyboard shortcuts here
        if (key == Key.Delete)
        {
            // Build the set of IDs to delete (multi-selection or single)
            int[] idsToDelete = [..ActiveTab.SelectedPieceIds];
            
            if (idsToDelete.Length > 0)
            {
                PushUndoSnapshot();
                foreach (var deleteId in idsToDelete)
                {
                    var piece = ActiveTab.ScenePieces.GetValueOrDefault(deleteId);
                    if (piece == null) continue;
                    if (ActiveTab.Stage != null)
                        for (int i = 0; i < ActiveTab.Stage.Pieces.Count; i++)
                            if (ActiveTab.Stage.Pieces[i] == piece.Obj) { ActiveTab.Stage.Pieces.RemoveAt(i); break; }
                    ActiveTab.ScenePieces.Remove(piece);
                    foreach (var grp in ActiveTab.HierarchyGroups) grp.PieceIds.Remove(piece.Id);
                }
                ActiveTab.SelectedPieceIds.Clear();
                ActiveTab.ActivePieceId = -1;
                ActiveTab.HasUnsavedChanges = true;
                RebuildClientRenderer();
            }
        }
        
        if (key == Key.S && _isCtrlPressed)
        {
            if (ActiveTab?.Stage != null) SaveStage();
        }
        
        if (key == Key.C && _isCtrlPressed && ActiveTab != null)
        {
            // Copy all selected pieces (or primary if no multi-selection)
            var ids = ActiveTab.SelectedPieceIds;
            if (ids.Count > 0)
            {
                var pieces = ActiveTab.ScenePieces.Where(p => ids.Contains(p.Id)).ToList();
                // Compute centroid so paste is relative
                var centroid = new f64Vector3(
                    (fix64)(pieces.Average(p => (double)p.Position.X)),
                    (fix64)(pieces.Average(p => (double)p.Position.Y)),
                    (fix64)(pieces.Average(p => (double)p.Position.Z)));
                _clipboard = pieces.Select(p => new ClipboardPiece(
                    p.Rad,
                    new f64Vector3(p.Position.X - centroid.X, p.Position.Y - centroid.Y, p.Position.Z - centroid.Z),
                    p.Rotation,
                    p.PiecePlacement.Type,
                    p.PiecePlacement.NodeKind,
                    p.PiecePlacement.IsSpecial
                )).ToList();
            }
        }
        
        if (key == Key.V && _isCtrlPressed && ActiveTab?.Stage != null && _clipboard.Count > 0)
        {
            PushUndoSnapshot();
            ActiveTab.SelectedPieceIds.Clear();
            // Determine paste offset — use snap size when enabled, otherwise a small fixed nudge
            float pasteOffsetXZ = _gridSnapEnabled && _gridSnapSize > 0f ? _gridSnapSize : 200f;
            var primaryPiece = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
            f64Vector3 pasteOrigin;
            if (primaryPiece != null)
            {
                float ox = (float)primaryPiece.Position.X + pasteOffsetXZ;
                float oz = (float)primaryPiece.Position.Z + pasteOffsetXZ;
                if (_gridSnapEnabled && _gridSnapSize > 0f)
                {
                    ox = MathF.Round(ox / _gridSnapSize) * _gridSnapSize;
                    oz = MathF.Round(oz / _gridSnapSize) * _gridSnapSize;
                }
                pasteOrigin = new f64Vector3((fix64)ox, primaryPiece.Position.Y, (fix64)oz);
            }
            else
            {
                pasteOrigin = f64Vector3.Zero;
            }
            int lastId = -1;
            foreach (var clip in _clipboard)
            {
                float wx = (float)pasteOrigin.X + (float)clip.RelativePosition.X;
                float wy = (float)pasteOrigin.Y + (float)clip.RelativePosition.Y;
                float wz = (float)pasteOrigin.Z + (float)clip.RelativePosition.Z;
                if (_gridSnapEnabled && _gridSnapSize > 0f)
                {
                    wx = MathF.Round(wx / _gridSnapSize) * _gridSnapSize;
                    wz = MathF.Round(wz / _gridSnapSize) * _gridSnapSize;
                }
                var worldPos = new f64Vector3((fix64)wx, (fix64)wy, (fix64)wz);
                var mesh = new StageObject(clip.Rad, worldPos, clip.Rotation, new PiecePlacement(clip.PlacementType, clip.Rad, worldPos, clip.Rotation, clip.AiNodeKind, IsSpecial: clip.IsSpecial));
                ActiveTab.Stage.Pieces[ActiveTab.Stage.StagePartCount] = mesh;
                var instance = new StagePieceInstance(mesh, ActiveTab.GetNextPieceId())
                {
                    Position = worldPos,
                    Rotation = clip.Rotation,
                };
                ActiveTab.ScenePieces.Add(instance);
                ActiveTab.SelectedPieceIds.Add(instance.Id);
                lastId = instance.Id;
            }
            if (lastId >= 0) ActiveTab.ActivePieceId = lastId;
            ActiveTab.HasUnsavedChanges = true;
            RebuildClientRenderer();
        }
        
        if (key == Key.Z && _isCtrlPressed)
        {
            PerformUndo();
        }
        
        if ((key == Key.Y && _isCtrlPressed) || (key == Key.Z && _isCtrlPressed && _isShiftPressed))
        {
            PerformRedo();
        }
        
        if (key == Key.Escape)
        {
            // Cancel placement mode, swap mode, and rect selection
            _pendingPlacementPartIndex = -1;
            _hasValidPlacementPos = false;
            _isSwapMode = false;
            _isRectSelecting = false;
        }
    }
    
    public override void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        if (imguiWantsKeyboard) return;
        if (!_isOpen) return;
        
        // Camera movement
        switch (key)
        {
            case Key.W:
                _moveForward = false;
                break;
            case Key.S:
                _moveBackward = false;
                break;
            case Key.A:
                _moveLeft = false;
                break;
            case Key.D:
                _moveRight = false;
                break;
            case Key.Space:
                _moveUp = false;
                break;
            case Key.Q:
                _moveDown = false;
                break;
            case Key.LShiftKey:
            case Key.RShiftKey:
                _isShiftPressed = false;
                break;
            case Key.LControlKey:
            case Key.RControlKey:
                _isCtrlPressed = false;
                break;
        }
    }
    
}
