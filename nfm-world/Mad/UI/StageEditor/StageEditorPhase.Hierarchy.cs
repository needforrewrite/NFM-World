using Maxine.Extensions;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    private void ReorderPiece(int draggedId, int targetId)
    {
        if (ActiveTab == null || draggedId == targetId) return;
        int draggedIndex = ActiveTab.ScenePieces.FindIndex(p => p.Id == draggedId);
        int targetIndex = ActiveTab.ScenePieces.FindIndex(p => p.Id == targetId);
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
}
