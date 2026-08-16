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
    private void ReorderPiece(int draggedId, int targetId)
    {
        if (ActiveTab == null || draggedId == targetId) return;
        int draggedIndex = ActiveTab.ScenePieces.FindIndex(p => p.Id == draggedId);
        int targetIndex  = ActiveTab.ScenePieces.FindIndex(p => p.Id == targetId);
        if (draggedIndex < 0 || targetIndex < 0) return;

        var dragged = ActiveTab.ScenePieces.ByIndex(draggedIndex);
        ActiveTab.ScenePieces.RemoveAt(draggedIndex);
        // Insert AFTER the target. When draggedIndex < targetIndex, the removal
        // shifted the target left by 1, so the target's new position is targetIndex-1.
        // Inserting at targetIndex places the dragged item right after the target.
        // When draggedIndex > targetIndex, the target didn't shift; insert at
        // targetIndex+1 to place after the target.
        int insertAt = draggedIndex < targetIndex ? targetIndex : targetIndex + 1;
        // Safety: if the key somehow still exists (e.g. duplicate from corrupted state),
        // remove the stale entry before inserting.
        if (ActiveTab.ScenePieces.Contains(dragged.Id))
        {
            ActiveTab.ScenePieces.Remove(dragged.Id);
        }
        ActiveTab.ScenePieces.Insert(insertAt, dragged);

        // ── Sync group membership when dragging across groups ──────────────────
        HierarchyGroup? srcGroup = ActiveTab.HierarchyGroups.FirstOrDefault(g => g.PieceIds.Contains(draggedId));
        HierarchyGroup? dstGroup = ActiveTab.HierarchyGroups.FirstOrDefault(g => g.PieceIds.Contains(targetId));
        if (srcGroup != dstGroup)
        {
            // Move piece from source group to destination group (or ungrouped if dstGroup == null)
            srcGroup?.PieceIds.Remove(draggedId);
            if (dstGroup != null)
            {
                int tpos = dstGroup.PieceIds.IndexOf(targetId);
                if (tpos < 0) dstGroup.PieceIds.Add(draggedId);
                else dstGroup.PieceIds.Insert(tpos + 1, draggedId);
            }
        }
        else if (srcGroup != null)
        {
            // Reorder within the same group
            srcGroup.PieceIds.Remove(draggedId);
            int tpos = srcGroup.PieceIds.IndexOf(targetId);
            if (tpos < 0) srcGroup.PieceIds.Add(draggedId);
            else srcGroup.PieceIds.Insert(tpos + 1, draggedId);
        }

        // Sync Stage.pieces order (only non-wall pieces end up there)
        if (ActiveTab.Stage != null)
        {
            ActiveTab.Stage.Pieces.Clear();
            foreach (var p in ActiveTab.ScenePieces)
                if (!p.PiecePlacement.IsWall)
                    ActiveTab.Stage.Pieces.Add(p.Obj);
        }

        ActiveTab.HasUnsavedChanges = true;
    }

    private void RenderHierarchy()
    {
        if (ActiveTab == null)
        {
            ImGui.Text("No stage loaded");
            return;
        }
        
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
        ImGui.Text($"Hierarchy — {ActiveTab.TabName}");
        ImGui.PopStyleColor();
        ImGui.Separator();
        
        // Hierarchy search filter
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##hiersearch", ref _hierarchySearch, 128);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Filter by name");
        ImGui.Spacing();
        
        bool hasFilter = !string.IsNullOrWhiteSpace(_hierarchySearch);
        
        // Stage Borders section
        bool bordersOpen = ImGui.TreeNodeEx("Stage Borders", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
        if (bordersOpen)
        {
            // Auto-update checkbox — when enabled, walls are recalculated automatically on piece changes
            bool autoWalls = _autoUpdateWalls;
            if (ImGui.Checkbox("Auto-update when pieces placed", ref autoWalls))
                _autoUpdateWalls = autoWalls;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, stage borders are automatically recalculated from placed pieces using the makeWalls algorithm.");

            ImGui.Spacing();
            RenderAutoGenerateBordersButton("_hierarchy");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var wall in ActiveTab.StageWalls)
            {
                string label = $"{wall.GetDisplayName()} ({wall.Count} walls)";
                if (hasFilter && !label.Contains(_hierarchySearch, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isSelected = wall.Id == ActiveTab.SelectedWallId;
                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.26f, 0.59f, 0.98f, 0.45f));

                if (ImGui.Selectable($"  {label}##wall{wall.Id}", isSelected, ImGuiSelectableFlags.SpanAllColumns))
                {
                    ActiveTab.SelectedWallId = wall.Id;
                    ActiveTab.ActivePieceId = -1;
                    ActiveTab.SelectedPieceIds.Clear();
                }

                if (isSelected)
                    ImGui.PopStyleColor();
            }
            ImGui.TreePop();
        }
        ImGui.Spacing();
        
        // All non-wall pieces
        var allPieces = ActiveTab.ScenePieces.Where(p => !p.PiecePlacement.IsWall).ToArray();
        var groupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
        var ungrouped = allPieces.Where(p => !groupedIds.Contains(p.Id)).ToArray();
        string ungroupedLabel = ActiveTab.HierarchyGroups.Count > 0 ? "Ungrouped" : "Pieces";
        
        int ungroupedSlot = ActiveTab.UngroupedOrderIndex >= 0
            ? Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count)
            : ActiveTab.HierarchyGroups.Count;
        
        // Deferred mutations — applied AFTER the render loop to avoid mid-loop ImGui stack corruption
        int pendingDeleteGrpId = -1;
        int pendingReorderFrom = -1;
        int pendingReorderInsertBefore = -1; // insert dragged group BEFORE this slot index
        
        // Render slots 0..Count inclusive; each slot has a drop-zone then optional content
        for (int ri = 0; ri <= ActiveTab.HierarchyGroups.Count; ri++)
        {
            // ── Drop zone before slot ri (thin invisible target for drag-reorder) ──
            ImGui.PushID($"dz_{ri}");
            ImGui.Dummy(new Vector2(-1f, 4f));
            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    var pl = ImGui.AcceptDragDropPayload("HIER_GROUP");
                    if (pl.Handle != null && pl.DataSize == sizeof(int))
                    {
                        int src = *(int*)pl.Data;
                        pendingReorderFrom = src;
                        pendingReorderInsertBefore = ri;
                    }
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.PopID();
            
            // ── Ungrouped block at this slot ──
            if (ri == ungroupedSlot)
                RenderHierarchyGroupFlat(ungroupedLabel, ungrouped, hasFilter);
            
            // ── Named group at slot ri ──
            if (ri < ActiveTab.HierarchyGroups.Count)
            {
                var group = ActiveTab.HierarchyGroups[ri];
                var gpPieces = group.PieceIds
                    .Select(id => allPieces.FirstOrDefault(p => p.Id == id)!)
                    .Where(p => p != null!)
                    .ToArray();
                
                ImGui.PushID($"grp{group.Id}");
                
                // ≡ drag handle — drag source for group reordering
                ImGui.SmallButton("=");
                if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    unsafe { int giv = ri; ImGui.SetDragDropPayload("HIER_GROUP", &giv, (nuint)sizeof(int)); }
                    ImGui.Text($"Moving: {group.Name}");
                    ImGui.EndDragDropSource();
                }
                ImGui.SameLine();
                
                bool open = ImGui.TreeNodeEx($"[G] {group.Name} ({gpPieces.Length})##grp{group.Id}",
                    ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
                
                // Accept piece drops onto this group header (right after TreeNodeEx so the target is on that item)
                if (ImGui.BeginDragDropTarget())
                {
                    unsafe
                    {
                        var pl = ImGui.AcceptDragDropPayload("HIER_PIECE");
                        if (pl.Handle != null && pl.DataSize == sizeof(int))
                        {
                            int srcId = *(int*)pl.Data;
                            foreach (var g2 in ActiveTab.HierarchyGroups) g2.PieceIds.Remove(srcId);
                            if (!group.PieceIds.Contains(srcId)) group.PieceIds.Add(srcId);
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Right-click context menu — after drag target so it doesn't steal the item
                if (ImGui.BeginPopupContextItem($"##grpctx{group.Id}"))
                {
                    ImGui.TextDisabled(group.Name);
                    ImGui.Separator();
                    if (ImGui.MenuItem("Rename..."))
                    {
                        _groupContextMenuGroupId = group.Id;
                        _renameGroupBuffer = group.Name;
                        _showRenameGroupDialog = true;
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.MenuItem("Select All in Group"))
                    {
                        ActiveTab.SelectedPieceIds.Clear();
                        foreach (var gp in gpPieces) ActiveTab.SelectedPieceIds.Add(gp.Id);
                        if (gpPieces.Length > 0) ActiveTab.ActivePieceId = gpPieces[^1].Id;
                        else ActiveTab.ActivePieceId = -1;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Delete Group (keep pieces)"))
                    {
                        pendingDeleteGrpId = group.Id; // deferred — not safe to remove here
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                
                // Render children — ALWAYS call TreePop when open==true
                if (open)
                {
                    RenderHierarchyGroup(gpPieces, hasFilter, group);
                    ImGui.TreePop();
                }
                
                ImGui.PopID();
            }
        }
        
        // ── Apply deferred mutations (safe: loop is finished, no ImGui stack mid-state) ──
        if (pendingDeleteGrpId >= 0)
        {
            ActiveTab.HierarchyGroups.RemoveAll(g => g.Id == pendingDeleteGrpId);
            if (ActiveTab.UngroupedOrderIndex >= 0)
                ActiveTab.UngroupedOrderIndex = Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count);
            ActiveTab.HasUnsavedChanges = true;
        }
        if (pendingReorderFrom >= 0 && pendingReorderInsertBefore >= 0 &&
            pendingReorderFrom != pendingReorderInsertBefore &&
            pendingReorderFrom != pendingReorderInsertBefore - 1) // not a no-op (insert right after itself)
        {
            var grp = ActiveTab.HierarchyGroups[pendingReorderFrom];
            ActiveTab.HierarchyGroups.RemoveAt(pendingReorderFrom);
            // After removal, insertion index shifts when src was before the target
            int insertIdx = pendingReorderInsertBefore > pendingReorderFrom
                ? pendingReorderInsertBefore - 1
                : pendingReorderInsertBefore;
            insertIdx = Math.Clamp(insertIdx, 0, ActiveTab.HierarchyGroups.Count);
            ActiveTab.HierarchyGroups.Insert(insertIdx, grp);
            if (ActiveTab.UngroupedOrderIndex >= 0)
                ActiveTab.UngroupedOrderIndex = Math.Clamp(ActiveTab.UngroupedOrderIndex, 0, ActiveTab.HierarchyGroups.Count);
            // Also reorder ScenePieces so save order matches visual order
            ReorderScenePiecesForGroupOrder();
            ActiveTab.HasUnsavedChanges = true;
        }
        
        // Rename group dialog (modal)
        if (_showRenameGroupDialog)
        {
            ImGui.SetNextWindowSize(new Vector2(300, 90), ImGuiCond.Always);
            ImGui.OpenPopup("##renamegroup");
        }
        if (ImGui.BeginPopupModal("##renamegroup", ref _showRenameGroupDialog, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize))
        {
            ImGui.Text("Rename Group:");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
            ImGui.InputText("##renbuf", ref _renameGroupBuffer, 64);
            if (ImGui.Button("OK", new Vector2(130, 0)))
            {
                var g = ActiveTab.HierarchyGroups.GetValueOrDefault(_groupContextMenuGroupId);
                if (g != null) { g.Name = _renameGroupBuffer; ActiveTab.HasUnsavedChanges = true; }
                _showRenameGroupDialog = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(-1, 0))) { _showRenameGroupDialog = false; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
        
        // "+ New Group" button
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Button("+ New Group from Selection", new Vector2(-1, 0)))
        {
            var newGroup = new HierarchyGroup { Id = ActiveTab.GetNextGroupId(), Name = "Group" };
            var selIds = ActiveTab.SelectedPieceIds;
            foreach (var sid in selIds)
            {
                foreach (var g in ActiveTab.HierarchyGroups) g.PieceIds.Remove(sid);
                newGroup.PieceIds.Add(sid);
            }
            ActiveTab.HierarchyGroups.Add(newGroup);
            ActiveTab.HasUnsavedChanges = true;
            // Open rename immediately
            _groupContextMenuGroupId = newGroup.Id;
            _renameGroupBuffer = newGroup.Name;
            _showRenameGroupDialog = true;
        }
    }
    
    // Reorders ScenePieces so pieces appear in group order (grouped first, then ungrouped).
    // Call after any group reorder so the save file reflects the visual hierarchy.
    private void ReorderScenePiecesForGroupOrder()
    {
        if (ActiveTab == null) return;
        var walls = ActiveTab.ScenePieces
            .Where(p => p.PiecePlacement.IsWall)
            .ToList();
        var nonWalls = ActiveTab.ScenePieces
            .Where(p => !p.PiecePlacement.IsWall)
            .ToList();
        var allGroupedIds = new HashSet<int>(ActiveTab.HierarchyGroups.SelectMany(g => g.PieceIds));
        var grouped = new List<StagePieceInstance>();
        foreach (var group in ActiveTab.HierarchyGroups)
        foreach (var id in group.PieceIds)
        {
            var piece = nonWalls.Find(p => p.Id == id);
            if (piece != null) grouped.Add(piece);
        }
        var ungroupedPieces = nonWalls.Where(p => !allGroupedIds.Contains(p.Id)).ToList();
        ActiveTab.ScenePieces.Clear();
        ActiveTab.ScenePieces.AddRange(walls);
        ActiveTab.ScenePieces.AddRange(grouped);
        ActiveTab.ScenePieces.AddRange(ungroupedPieces);
    }
    
    // Renders a list of pieces with Ctrl+click multi-select, drag-drop reorder, and right-click group context menu
    private void RenderHierarchyGroup(StagePieceInstance[] pieces, bool hasFilter, HierarchyGroup? owningGroup = null)
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            string typeTag = piece.PiecePlacement.Type switch
            {
                PiecePlacementType.CheckPoint => " [Chk]",
                PiecePlacementType.FixHoop => " [Fix]",
                _ => " [Set]"
            };
            string displayName = $"{piece.Name}{typeTag} (ID: {piece.Id})";
            if (hasFilter && !displayName.Contains(_hierarchySearch, StringComparison.OrdinalIgnoreCase))
                continue;
            
            bool isSelected = ActiveTab!.SelectedPieceIds.Contains(piece.Id);
            if (isSelected)
                ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.26f, 0.59f, 0.98f, 0.45f));
            
            string rowLabel = _hierDragSourceId == piece.Id
                ? $"\u2195 {displayName}##piece{piece.Id}"
                : $"  {displayName}##piece{piece.Id}";
            
            if (ImGui.Selectable(rowLabel, isSelected, ImGuiSelectableFlags.SpanAllColumns))
            {
                if (_isCtrlPressed)
                {
                    if (!ActiveTab.SelectedPieceIds.Remove(piece.Id))
                        ActiveTab.SelectedPieceIds.Add(piece.Id);
                }
                else
                {
                    ActiveTab.SelectedPieceIds.Clear();
                    ActiveTab.SelectedPieceIds.Add(piece.Id);
                }

                ActiveTab.ActivePieceId = piece.Id;
                ActiveTab.SelectedWallId = -1;
            }
            
            if (isSelected) ImGui.PopStyleColor();
            
            // Right-click: group management
            if (ImGui.BeginPopupContextItem($"##piecectx{piece.Id}"))
            {
                ImGui.TextDisabled(piece.Name);
                ImGui.Separator();
                if (owningGroup != null && ImGui.MenuItem("Remove from Group"))
                {
                    owningGroup.PieceIds.Remove(piece.Id);
                    ActiveTab!.HasUnsavedChanges = true;
                    ImGui.EndPopup();
                    continue;
                }
                if (ActiveTab!.HierarchyGroups.Count > 0 && ImGui.BeginMenu("Move to Group"))
                {
                    foreach (var g in ActiveTab.HierarchyGroups)
                    {
                        if (g == owningGroup) continue;
                        if (ImGui.MenuItem(g.Name))
                        {
                            if (owningGroup != null) owningGroup.PieceIds.Remove(piece.Id);
                            if (!g.PieceIds.Contains(piece.Id)) g.PieceIds.Add(piece.Id);
                            ActiveTab.HasUnsavedChanges = true;
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndPopup();
            }
            
            // Drag source
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                _hierDragSourceId = piece.Id;
                unsafe
                {
                    int dragId = piece.Id;
                    ImGui.SetDragDropPayload("HIER_PIECE", &dragId, sizeof(int));
                }
                ImGui.TextUnformatted($"\u2195 Move: {displayName}");
                ImGui.EndDragDropSource();
            }
            
            // Drop target: reorder
            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    var payload = ImGui.AcceptDragDropPayload("HIER_PIECE");
                    if (payload.Handle != null && payload.DataSize == sizeof(int))
                    {
                        int sourceId = *(int*)payload.Data;
                        if (sourceId != piece.Id)
                        {
                            PushUndoSnapshot();
                            ReorderPiece(sourceId, piece.Id);
                        }
                    }
                }
                _hierDragSourceId = -1;
                ImGui.EndDragDropTarget();
            }
        }
    }
    
    // Wrapper: renders under a collapsible TreeNode header (for "Pieces" / "Ungrouped")
    private void RenderHierarchyGroupFlat(string label, StagePieceInstance[] pieces, bool hasFilter)
    {
        if (pieces.Length == 0 && !hasFilter) return;
        bool open = ImGui.TreeNodeEx($"{label} ({pieces.Length})", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
        // The tree-node item itself is a drop target: dropping a piece here removes it from any group
        if (ImGui.BeginDragDropTarget())
        {
            unsafe
            {
                var pl = ImGui.AcceptDragDropPayload("HIER_PIECE");
                if (pl.Handle != null && pl.DataSize == sizeof(int))
                {
                    int srcId = *(int*)pl.Data;
                    foreach (var g in ActiveTab!.HierarchyGroups) g.PieceIds.Remove(srcId);
                    ActiveTab!.HasUnsavedChanges = true;
                }
            }
            ImGui.EndDragDropTarget();
        }
        if (open)
        {
            RenderHierarchyGroup(pieces, hasFilter, null);
            ImGui.TreePop();
        }
    }
}
