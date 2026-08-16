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
    private void RenderViewport()
    {
        // Viewport tabs
        if (ImGui.BeginTabBar("ViewportTabs"))
        {
            if (ImGui.BeginTabItem("Scene"))
            {
                if (ActiveTab != null && ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.Scene)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.Scene;
                    UpdateCameraPosition();
                }
                
                ImGui.Text("3D Scene View");
                if (ActiveTab != null)
                {
                    ImGui.Text($"Camera: Yaw={ActiveTab.CameraYaw:F1}° Pitch={ActiveTab.CameraPitch:F1}° Dist={ActiveTab.CameraDistance:F0}");
                }
                ImGui.Text($"Pieces in scene: {ActiveTab.ScenePieces.Count}");
                
                // The actual 3D rendering happens in Render3D()
                
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Top Down"))
            {
                if (ActiveTab != null && ActiveTab.ViewMode != StageEditorTab.ViewModeEnum.TopDown)
                {
                    ActiveTab.ViewMode = StageEditorTab.ViewModeEnum.TopDown;
                    UpdateCameraPosition();
                }
                
                ImGui.Text("Top Down View");
                ImGui.Text($"Pieces in scene: {ActiveTab.ScenePieces.Count}");
                
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    private void RenderInspector()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
        ImGui.Text("Inspector");
        ImGui.PopStyleColor();
        ImGui.Separator();
        
        if (ActiveTab == null) return;
        
        // Wall selected
        if (ActiveTab.SelectedWallId >= 0)
        {
            var wall = ActiveTab.StageWalls.GetValueOrDefault(ActiveTab.SelectedWallId);
            if (wall != null)
            {
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 1f, 1f), wall.GetDisplayName());
                ImGui.Spacing();
                
                if (ImGui.CollapsingHeader("Border Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Spacing();
                    var count = wall.Count;
                    ImGui.SetNextItemWidth(-1);
                    bool countChanged = ImGui.DragInt("Wall Count##wc", ref count, 1f, 1, 100);
                    if (ImGui.IsItemActivated() && !_inspectorWallDragging)
                    {
                        _inspectorWallDragging = true;
                        PushUndoSnapshot();
                    }
                    if (countChanged)
                    {
                        if (wall.Count != count) { wall.Count = count; ActiveTab.HasUnsavedChanges = true; RebuildAllWalls(); }
                    }
                    if (ImGui.IsItemDeactivated())
                        _inspectorWallDragging = false;

                    var pos = wall.Position;
                    ImGui.SetNextItemWidth(-1);
                    bool posChanged = ImGui.DragInt("Position##wp", ref pos, 10f);
                    if (ImGui.IsItemActivated() && !_inspectorWallDragging)
                    {
                        _inspectorWallDragging = true;
                        PushUndoSnapshot();
                    }
                    if (posChanged)
                    {
                        if (wall.Position != pos) { wall.Position = pos; ActiveTab.HasUnsavedChanges = true; RebuildAllWalls(); }
                    }
                    if (ImGui.IsItemDeactivated())
                        _inspectorWallDragging = false;

                    var offset = wall.Offset;
                    ImGui.SetNextItemWidth(-1);
                    bool offsetChanged = ImGui.DragInt("Offset##wo", ref offset, 10f);
                    if (ImGui.IsItemActivated() && !_inspectorWallDragging)
                    {
                        _inspectorWallDragging = true;
                        PushUndoSnapshot();
                    }
                    if (offsetChanged)
                    {
                        if (wall.Offset != offset) { wall.Offset = offset; ActiveTab.HasUnsavedChanges = true; RebuildAllWalls(); }
                    }
                    if (ImGui.IsItemDeactivated())
                        _inspectorWallDragging = false;

                    ImGui.Spacing();
                    RenderAutoGenerateBordersButton("_selectedwall");
                    ImGui.Spacing();
                }
                
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Border", new Vector2(-1, 0)))
                {
                    PushUndoSnapshot();
                    ActiveTab.StageWalls.Remove(wall);
                    ActiveTab.SelectedWallId = -1;
                    ActiveTab.HasUnsavedChanges = true;
                    RebuildAllWalls();
                }
                ImGui.PopStyleColor();
            }
            return;
        }
        
        // Piece selected
        if (ActiveTab.ActivePieceId >= 0)
        {
            // Multi-selection banner
            int selCount = ActiveTab.SelectedPieceIds.Count;
            if (selCount > 1)
            {
                ImGui.TextColored(new Vector4(0.26f, 0.8f, 0.98f, 1f), $"{selCount} pieces selected");
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete All Selected", new Vector2(-1, 0)))
                {
                    PushUndoSnapshot();
                    int[] toDelete = [..ActiveTab.SelectedPieceIds];
                    foreach (var did in toDelete)
                    {
                        var dp = ActiveTab.ScenePieces.GetValueOrDefault(did);
                        if (dp == null) continue;
                        if (ActiveTab.Stage != null)
                            for (int si = 0; si < ActiveTab.Stage.Pieces.Count; si++)
                                if (ActiveTab.Stage.Pieces[si] == dp.Obj) { ActiveTab.Stage.Pieces.RemoveAt(si); break; }
                        ActiveTab.ScenePieces.Remove(dp);
                        foreach (var grp in ActiveTab.HierarchyGroups) grp.PieceIds.Remove(dp.Id);
                    }
                    ActiveTab.SelectedPieceIds.Clear();
                    ActiveTab.ActivePieceId = -1;
                    ActiveTab.HasUnsavedChanges = true;
                    RebuildClientRenderer();
                }
                ImGui.PopStyleColor();
                ImGui.Spacing();
                ImGui.TextDisabled("(showing primary piece below)");
                ImGui.Separator();
                ImGui.Spacing();
            }
            
            var piece = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
            if (piece != null)
            {
                string shortName = piece.Name.Contains('/') ? piece.Name.Substring(piece.Name.LastIndexOf('/') + 1) : piece.Name;
                ImGui.TextColored(new Vector4(0.7f, 1f, 0.7f, 1f), shortName);
                ImGui.TextDisabled(piece.Name);
                ImGui.Spacing();
                
                // Transform section
                if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Spacing();
                    // Position: display Y offset by 250 so ground=0 for the user
                    var displayPos = new Vector3((float)piece.Position.X, (float)piece.Position.Y - 250, (float)piece.Position.Z);
                    ImGui.Text("Position");
                    ImGui.SetNextItemWidth(-1);
                    bool posDragging = ImGui.DragFloat3("##pos", ref displayPos, 10f);
                    // Check activation state independent of value change
                    if (ImGui.IsItemActivated() && !_inspectorPosDragging)
                    {
                        _inspectorPosDragging = true;
                        PushUndoSnapshot();
                    }
                    if (posDragging)
                    {
                        var newPos = new f64Vector3((fix64)displayPos.X, (fix64)(displayPos.Y + 250), (fix64)displayPos.Z);
                        var deltaX = newPos.X - piece.Position.X;
                        var deltaY = newPos.Y - piece.Position.Y;
                        var deltaZ = newPos.Z - piece.Position.Z;
                        piece.Position = newPos;
                        // Apply same delta to all other selected pieces
                        foreach (var selId in ActiveTab.SelectedPieceIds)
                        {
                            if (selId == piece.Id) continue;
                            var sp = ActiveTab.ScenePieces.GetValueOrDefault(selId);
                            sp?.Position = new f64Vector3(sp.Position.X + deltaX, sp.Position.Y + deltaY, sp.Position.Z + deltaZ);
                        }
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    // Clear flag when drag ends
                    if (ImGui.IsItemDeactivated())
                        _inspectorPosDragging = false;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Y=0 is ground level (internal Y=250).");
                    
                    ImGui.Text("Rotation (Yaw)");
                    float rotY = (float)piece.Rotation.Yaw.Degrees;
                    ImGui.SetNextItemWidth(-1);
                    bool rotDragging = ImGui.DragFloat("##roty", ref rotY, 1f, -180f, 180f);
                    // Check activation state independent of value change
                    if (ImGui.IsItemActivated() && !_inspectorRotDragging)
                    {
                        _inspectorRotDragging = true;
                        PushUndoSnapshot();
                    }
                    if (rotDragging)
                    {
                        float rotDelta = rotY - (float)piece.Rotation.Yaw.Degrees;
                        
                        // For grouped pieces, rotate positions around the centroid (same as gizmo)
                        if (ActiveTab.SelectedPieceIds.Count > 1)
                        {
                            // Calculate centroid of all selected pieces BEFORE any rotations
                            float centroidX = 0f, centroidZ = 0f;
                            foreach (var selId in ActiveTab.SelectedPieceIds)
                            {
                                var sp = ActiveTab.ScenePieces.GetValueOrDefault(selId);
                                if (sp != null)
                                {
                                    centroidX += (float)sp.Position.X;
                                    centroidZ += (float)sp.Position.Z;
                                }
                            }
                            centroidX /= ActiveTab.SelectedPieceIds.Count;
                            centroidZ /= ActiveTab.SelectedPieceIds.Count;
                            
                            // Rotate ALL pieces (including active) around centroid
                            float radians = rotDelta * MathF.PI / 180f;
                            float cosA = MathF.Cos(radians);
                            float sinA = MathF.Sin(radians);
                            
                            foreach (var selId in ActiveTab.SelectedPieceIds)
                            {
                                var sp = ActiveTab.ScenePieces.GetValueOrDefault(selId);
                                if (sp != null)
                                {
                                    float relX = (float)sp.Position.X - centroidX;
                                    float relZ = (float)sp.Position.Z - centroidZ;
                                    float newRelX = relX * cosA - relZ * sinA;
                                    float newRelZ = relX * sinA + relZ * cosA;
                                    sp.Position = new f64Vector3(
                                        (fix64)(centroidX + newRelX),
                                        sp.Position.Y,
                                        (fix64)(centroidZ + newRelZ));
                                    sp.Rotation = new f64Euler(f64AngleSingle.FromDegrees((fix64)(((float)sp.Rotation.Yaw.Degrees + rotDelta) % 360f)), sp.Rotation.Pitch, sp.Rotation.Roll);
                                }
                            }
                        }
                        else
                        {
                            // Single piece: just update rotation without position change
                            piece.Rotation = new f64Euler(f64AngleSingle.FromDegrees((fix64)rotY), piece.Rotation.Pitch, piece.Rotation.Roll);
                        }
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    // Clear flag when drag ends
                    if (ImGui.IsItemDeactivated())
                        _inspectorRotDragging = false;
                    ImGui.Spacing();
                }
                
                // Piece settings section
                if (ImGui.CollapsingHeader("Piece Settings", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Spacing();
                    ImGui.Text("Type");
                    int pieceType = (int)piece.PiecePlacement.Type;
                    string[] typeNames = ["Object", "Checkpoint", "Fix Hoop"];
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo("##type", ref pieceType, typeNames, typeNames.Length))
                    {
                        piece.PiecePlacement = piece.PiecePlacement with { Type = (PiecePlacementType)pieceType };
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    
                    ImGui.Text("AI Tags");
                    ImGui.SetNextItemWidth(-1);

                    int nodeType = piece.PiecePlacement.NodeKind is not {} value ? 0 : (int)(value + 1);
                    string[] nodeTypeNames = [
                        "None (Ignore)",
                        "Fake CheckPoint",
                        "Road",
                        "Turn",
                        "Auto",
                        "Ramp",
                        "Halfpipe",
                        "Sequence Start",
                        "Sequence End",
                        "Fix Road Start",
                        "Fix Ramp",
                        "Fix Hoop",
                        "Fix Road End",
                        "Avoid",
                        "Reset"
                    ];
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.Combo("##aitype", ref nodeType, nodeTypeNames, nodeTypeNames.Length))
                    {
                        piece.PiecePlacement = piece.PiecePlacement with { NodeKind = pieceType == 0 ? null : (AiNodeKind)(pieceType - 1) };
                        ActiveTab.HasUnsavedChanges = true;
                    }
                    
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(
                            """
                            checkpoint -> same as chk()
                            road -> same as )p
                            turn -> same as )pt
                            auto -> road if road, ramp if ramp
                            ramp -> same as )pr
                            halfpipe -> same as )ph
                            sequencestart -> cannot be skipped, when the ai hits it will go through every node until sequenceend
                            sequenceend -> ends a sequence
                            fixroadstart -> when the ai hits it when needing to fix it will go throguh every node until it hits a fixhoop or fixroadend
                            fixroadend -> same thing but can hit it in the opposite direction (backwards)
                            avoid -> AI will try to dodge this node if it is nearby it
                            reset -> i forgor lol
                            """);
                    ImGui.Spacing();
                    
                    if (!_isSwapMode)
                    {
                        // Only allow swap when all selected pieces share the same model
                        bool swapAllowed = true;
                        if (ActiveTab.SelectedPieceIds.Count > 1)
                        {
                            var allSelected = ActiveTab.ScenePieces
                                .Where(p => ActiveTab.SelectedPieceIds.Contains(p.Id)).ToList();
                            var firstName = allSelected.Count > 0 ? allSelected[0].Name : piece.Name;
                            swapAllowed = allSelected.All(p => p.Name == firstName);
                        }
                        if (!swapAllowed) ImGui.BeginDisabled();
                        if (ImGui.Button("Swap Piece...", new Vector2(-1, 0)))
                        {
                            _isSwapMode = true;
                            _pendingPlacementPartIndex = -1;
                        }
                        if (!swapAllowed)
                        {
                            ImGui.EndDisabled();
                            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                                ImGui.SetTooltip("All selected pieces must use the same model to swap");
                        }
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.4f, 0.05f, 1f));
                        if (ImGui.Button("Cancel Swap", new Vector2(-1, 0)))
                            _isSwapMode = false;
                        ImGui.PopStyleColor();
                    }
                    ImGui.Spacing();
                }
                
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Piece", new Vector2(-1, 0)))
                {
                    if (ActiveTab.Stage != null)
                    {
                        for (int i = 0; i < ActiveTab.Stage.Pieces.Count; i++)
                        {
                            if (ActiveTab.Stage.Pieces[i] == piece.Obj)
                            {
                                ActiveTab.Stage.Pieces.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    ActiveTab.ScenePieces.Remove(piece);
                    ActiveTab.SelectedPieceIds.Remove(piece.Id);
                    ActiveTab.ActivePieceId = -1;
                    ActiveTab.HasUnsavedChanges = true;
                    RebuildClientRenderer();
                }
                ImGui.PopStyleColor();
                return;
            }
        }
        
        // Nothing selected
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Borders", ImGuiTreeNodeFlags.DefaultOpen))
        {
            RenderAutoGenerateBordersButton("_noselection");
            ImGui.Spacing();
        }
        ImGui.TextDisabled("No piece selected.");
        ImGui.TextDisabled("Click a piece in the viewport");
        ImGui.TextDisabled("or select from the Hierarchy.");
    }
    
    private void RenderPartsLibrary()
    {
        // Header row: title + counts
        if (_isSwapMode)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0.1f, 1f));
            ImGui.Text("Stage Parts Library");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.1f, 1f), "— Click a part to swap. [Esc] to cancel.");
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
            ImGui.Text("Stage Parts Library");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextDisabled($"({_availableParts.Count} parts)");
        }
        ImGui.Separator();
        
        // Search bar
        float searchWidth = ImGui.GetContentRegionAvail().X - 4;
        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputText("##partssearch", ref _partsSearch, 128);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Search parts by name");
        ImGui.Spacing();
        
        // Category filter tabs
        string[] catLabels = ["All", "NFMM", "Vendor", "User"];
        for (int c = 0; c < catLabels.Length; c++)
        {
            if (c > 0) ImGui.SameLine();
            bool active = _partsCategory == c;
            if (active)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.26f, 0.59f, 0.98f, 0.7f));
            if (ImGui.SmallButton(catLabels[c]))
                _partsCategory = c;
            if (active)
                ImGui.PopStyleColor();
        }
        
        // Snap + rotation controls (right-aligned in same row)
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        
        bool snapOn = _snapEnabled;
        if (snapOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.65f, 0.2f, 0.8f));
        if (ImGui.SmallButton(_snapEnabled ? "Snap ON" : "Snap OFF"))
            _snapEnabled = !_snapEnabled;
        if (snapOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle snapping to preset attachment points.");

        // Grid snap toggle
        bool gridSnapOn = _gridSnapEnabled;
        if (gridSnapOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.65f, 0.2f, 0.8f));
        if (ImGui.SmallButton(_gridSnapEnabled ? $"[G] Grid ON: {_gridSnapSize:F0}" : "[G] Grid OFF"))
            _gridSnapEnabled = !_gridSnapEnabled;
        if (gridSnapOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle grid snapping (G).\nScroll wheel cycles snap size when in placement mode.");
        
        if (_gridSnapEnabled)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80f);
            if (ImGui.BeginCombo("##snapsize", $"{_gridSnapSize:F0}"))
            {
                for (int si = 0; si < SnapPresets.Length; si++)
                {
                    bool sel = si == _snapPresetIndex;
                    if (ImGui.Selectable($"{SnapPresets[si]:F0}", sel))
                    {
                        _snapPresetIndex = si;
                        _gridSnapSize = SnapPresets[si];
                    }
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Grid snap size in world units.\nRoad spacing = 5600.");
        }
        
        if (_pendingPlacementPartIndex >= 0)
        {
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.1f, 0.9f, 1.0f, 1.0f), $"Yaw: {_pendingPlacementYaw:F0}°");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("[Q] -45°   [E] +45°   [R] Reset");
            ImGui.SameLine();
            if (ImGui.SmallButton("-45")) _pendingPlacementYaw = ((_pendingPlacementYaw - 45f) % 360f + 360f) % 360f;
            ImGui.SameLine();
            if (ImGui.SmallButton("+45")) _pendingPlacementYaw = (_pendingPlacementYaw + 45f) % 360f;
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset##rot")) _pendingPlacementYaw = 0f;
        }
        
        ImGui.Separator();
        
        // Build filtered list
        bool hasSearchFilter = !string.IsNullOrWhiteSpace(_partsSearch);
        
        ImGui.BeginChild("##partsgrid", new Vector2(0, 0), ImGuiChildFlags.None);
        
        const float tileImgSize = 56f;  // image / button area height
        const float tileLabelH  = 16f;  // label height below image
        const float tileW       = 64f;  // total tile width
        const float tileH       = tileImgSize + tileLabelH + 4f;
        const float tilePad     = 6f;
        
        float regionW = ImGui.GetContentRegionAvail().X;
        int   cols    = Math.Max(1, (int)(regionW / (tileW + tilePad)));
        int   col     = 0;
        
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(tilePad, tilePad));
        
        for (int i = 0; i < _availableParts.Count; i++)
        {
            var part = _availableParts[i];
            
            // Category filter
            bool inCategory = _partsCategory switch
            {
                1 => part.FileName.StartsWith("nfmm/"),
                2 => part.FileName.StartsWith("nfmw/") || part.FileName.StartsWith("vendor/"),
                3 => part.FileName.StartsWith("user/"),
                _ => true
            };
            if (!inCategory) continue;
            
            // Name search
            if (hasSearchFilter && !part.FileName.Contains(_partsSearch, StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Queue preview generation if not yet done
            QueuePartPreview(part.FileName, part);
            
            if (col > 0)
                ImGui.SameLine(0, tilePad);
            
            var groupTopLeft = ImGui.GetCursorScreenPos();
            bool isPendingPlacement = i == _pendingPlacementPartIndex;
            
            // In swap mode, determine if this tile is the current piece
            bool isCurrentSwapPiece = false;
            if (_isSwapMode && ActiveTab != null && ActiveTab.ActivePieceId >= 0)
            {
                var sp = ActiveTab.ScenePieces.GetValueOrDefault(ActiveTab.ActivePieceId);
                isCurrentSwapPiece = sp != null && sp.Name == part.FileName;
            }
            ImGui.PushID(i);
            ImGui.BeginGroup();
            
            bool clicked;
            if (_partPreviews.TryGetValue(part.FileName, out var preview))
            {
                // Show 3D thumbnail
                // Flip UVs vertically — FNA (OpenGL) render targets are stored bottom-up
                ImGui.Image(preview.Ref, new Vector2(tileW, tileImgSize),
                    new Vector2(0, 1), new Vector2(1, 0));
                clicked = ImGui.IsItemClicked();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(part.FileName);
            }
            else
            {
                // Fallback: colored button while preview is loading
                var tileColor = GetPartTileColor(part.FileName);
                ImGui.PushStyleColor(ImGuiCol.Button, tileColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(
                    Math.Min(tileColor.X + 0.15f, 1f),
                    Math.Min(tileColor.Y + 0.15f, 1f),
                    Math.Min(tileColor.Z + 0.15f, 1f),
                    tileColor.W));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.26f, 0.59f, 0.98f, 0.9f));
                clicked = ImGui.Button("##tile", new Vector2(tileW, tileImgSize));
                ImGui.PopStyleColor(3);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(part.FileName);
            }
            
            // Label centered under thumbnail
            string shortName = part.FileName.Contains('/') ? part.FileName[(part.FileName.LastIndexOf('/') + 1)..] : part.FileName;
            var textSize = ImGui.CalcTextSize(shortName, false, tileW);
            float textOffX = Math.Max(0, (tileW - textSize.X) * 0.5f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + textOffX);
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textSize.X);
            ImGui.TextUnformatted(shortName);
            ImGui.PopTextWrapPos();
            
            ImGui.EndGroup();
            
            // Highlight border when this part is selected for placement
            if (isPendingPlacement)
            {
                var drawList = ImGui.GetWindowDrawList();
                uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.9f, 1.0f, 1.0f));
                drawList.AddRect(
                    groupTopLeft,
                    new Vector2(groupTopLeft.X + tileW, groupTopLeft.Y + tileH),
                    borderColor, 2f, ImDrawFlags.None, 2.5f
                );
            }
            
            // Highlight the current piece when in swap mode
            if (isCurrentSwapPiece)
            {
                var drawList = ImGui.GetWindowDrawList();
                uint borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.75f, 0.1f, 1.0f));
                drawList.AddRect(
                    groupTopLeft,
                    new Vector2(groupTopLeft.X + tileW, groupTopLeft.Y + tileH),
                    borderColor, 2f, ImDrawFlags.None, 2.5f
                );
            }
            
            ImGui.PopID();
            
            if (clicked)
            {
                if (_isSwapMode && ActiveTab != null && ActiveTab.ActivePieceId >= 0)
                {
                    // Swap the selected piece to this part
                    var swapPieceIdx = ActiveTab.ScenePieces.FindIndex(p => p.Id == ActiveTab.ActivePieceId);
                    if (swapPieceIdx != -1)
                    {
                        var swapPiece = ActiveTab.ScenePieces.ByIndex(swapPieceIdx);
                        
                        if (part != swapPiece.Rad)
                        {
                            PushUndoSnapshot();
                            var newRot = swapPiece.Rotation;
                            var newMesh = StageObject.CreateDefaultObject(part, swapPiece.Position, newRot);
                            if (ActiveTab.Stage != null)
                            {
                                for (int si = 0; si < ActiveTab.Stage.Pieces.Count; si++)
                                {
                                    if (ActiveTab.Stage.Pieces[si] == swapPiece.Obj)
                                    {
                                        ActiveTab.Stage.Pieces[si] = newMesh;
                                        break;
                                    }
                                }
                            }

                            ActiveTab.ScenePieces.Swap(swapPieceIdx, new StagePieceInstance(newMesh, swapPiece.Id));
                            ActiveTab.HasUnsavedChanges = true;
                            RebuildClientRenderer();
                        }
                    }
                    _isSwapMode = false;
                }
                else
                {
                    // Enter placement mode: the user will click in the viewport to place the part
                    _pendingPlacementPartIndex = i;
                    _hasValidPlacementPos = false;
                }
            }
            
            col++;
            if (col >= cols) col = 0;
        }
        
        ImGui.PopStyleVar(); // ItemSpacing
        ImGui.EndChild();
    }
    
    private static Vector4 GetPartTileColor(string name)
    {
        if (name.Contains("checkpoint") || name.Contains("chk"))
            return new Vector4(0.18f, 0.55f, 0.18f, 0.85f); // green
        if (name.Contains("fix") || name.Contains("hoop"))
            return new Vector4(0.65f, 0.20f, 0.20f, 0.85f); // red
        if (name.Contains("road") || name.Contains("ramp") || name.Contains("roll"))
            return new Vector4(0.38f, 0.30f, 0.20f, 0.85f); // brown
        if (name.Contains("wall") || name.Contains("border"))
            return new Vector4(0.25f, 0.25f, 0.45f, 0.85f); // blue-grey
        if (name.Contains("turn") || name.Contains("twist") || name.Contains("bend"))
            return new Vector4(0.45f, 0.30f, 0.10f, 0.85f); // orange-brown
        return new Vector4(0.25f, 0.30f, 0.38f, 0.85f); // default slate
    }
}
